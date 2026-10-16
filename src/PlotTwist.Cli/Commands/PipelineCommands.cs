namespace PlotTwist.Cli.Commands;

using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PlotTwist.Application.Abstractions;
using PlotTwist.Application.Embedding;
using PlotTwist.Application.Import;
using PlotTwist.Application.Verification;
using PlotTwist.Infrastructure.Configuration;

// The ingestion pipeline, one command per step so each can be re-run on its own:
//   migrate -> import -> embed -> index -> verify      (or `ingest` for all of them in order)
internal static class PipelineCommands
{
    public static IEnumerable<Command> Create(IServiceProvider services)
    {
        var limit = new Option<int?>("--limit") { Description = "Only the first N films (a quicker demo database)." };
        var rebuild = new Option<bool>("--rebuild") { Description = "Drop and rebuild the vector index." };

        var migrate = new Command("migrate", "Apply database schema migrations (FluentMigrator).");
        migrate.SetAction(async (_, ct) => await Migrate(services, ct));

        var import = new Command("import", "Fetch the pinned dataset revision, verify it, stage the raw rows, and upsert films. Rejected rows are recorded.") { limit };
        import.SetAction(async (parse, ct) => await Import(services, parse.GetValue(limit), ct));

        var embed = new Command("embed", "Embed films that are new or changed for the configured model. Resumable: stop it any time.") { limit };
        embed.SetAction(async (parse, ct) => await Embed(services, parse.GetValue(limit), ct));

        var index = new Command("index", "Build the HNSW index for the configured model's vectors (after a bulk embed).") { rebuild };
        index.SetAction(async (parse, ct) => await Index(services, parse.GetValue(rebuild), ct));

        var verify = new Command("verify", "Check that the numbers add up: import accounted for, everything embedded, index present.");
        verify.SetAction(async (_, ct) => await Verify(services, ct));

        var ingest = new Command("ingest", "Run migrate, import, embed, index and verify in order.") { limit };
        ingest.SetAction(async (parse, ct) =>
        {
            await Migrate(services, ct);
            await Import(services, parse.GetValue(limit), ct);
            await Embed(services, null, ct);
            await Index(services, rebuild: false, ct);
            return await Verify(services, ct);
        });

        return [migrate, import, embed, index, verify, ingest];
    }

    private static async Task<int> Migrate(IServiceProvider services, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var applied = await scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>().MigrateUp(ct);
        Console.WriteLine(applied ? "migrate: schema updated." : "migrate: schema already up to date.");
        return 0;
    }

    private static async Task<int> Import(IServiceProvider services, int? limit, CancellationToken ct)
    {
        Console.WriteLine("import: fetching and verifying the pinned dataset ...");
        var result = await services.GetRequiredService<ImportMoviesHandler>().Handle(new ImportMovies(limit), ct);
        Console.WriteLine($"import: {result.Source}@{result.Revision[..8]} (run {result.RunId}) in {result.Elapsed.TotalSeconds:N1}s");
        Console.WriteLine($"        staged {result.Staged:N0} rows -> {result.Imported:N0} films ({result.New:N0} new), {result.Rejected:N0} rows rejected (see movie_rejects)");
        return 0;
    }

    private static async Task<int> Embed(IServiceProvider services, int? limit, CancellationToken ct)
    {
        var options = services.GetRequiredService<EmbeddingOptions>();
        var progress = new Progress<EmbedProgress>(p =>
            Console.Write($"\r  {p.Embedded,6:N0} / {p.Total:N0}  ({p.PerSecond:N0}/s, ~{(p.Total - p.Embedded) / Math.Max(p.PerSecond, 0.001):N0}s left)   "));

        var result = await services.GetRequiredService<EmbedMoviesHandler>()
            .Handle(new EmbedMovies(options.BatchSize, options.MaxConcurrency, limit), progress, ct);
        Console.WriteLine(result.Embedded == 0
            ? $"embed: nothing pending for {result.Model}."
            : $"\nembed: {result.Embedded:N0} of {result.Pending:N0} pending films embedded with {result.Model} in {result.Elapsed:mm\\:ss} (run {result.RunId}).");
        return 0;
    }

    private static async Task<int> Index(IServiceProvider services, bool rebuild, CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        var status = await services.GetRequiredService<BuildVectorIndexHandler>().Handle(new BuildVectorIndex(rebuild), ct);
        Console.WriteLine($"index: {status.Name} ({status.Size}) ready in {(DateTime.UtcNow - started).TotalSeconds:N1}s.");
        return 0;
    }

    private static async Task<int> Verify(IServiceProvider services, CancellationToken ct)
    {
        var report = await services.GetRequiredService<VerifyIngestionHandler>().Handle(ct);
        Console.WriteLine($"verify: {report.Model}");
        foreach (var check in report.Checks)
        {
            Console.ForegroundColor = check.Passed ? ConsoleColor.Green : ConsoleColor.Red;
            Console.Write(check.Passed ? "  PASS " : "  FAIL ");
            Console.ResetColor();
            Console.WriteLine($"{check.Name}: {check.Detail}");
        }

        if (report.Passed)
            Console.WriteLine("Next: run the retrieval evaluation before pointing the Api at a new model.");
        return report.Passed ? 0 : 1;
    }
}
