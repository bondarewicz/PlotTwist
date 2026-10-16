namespace PlotTwist.Cli.Commands;

using System.CommandLine;
using Application.Abstractions;
using Application.Embedding;
using Application.Import;
using Application.Verification;
using Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

internal static class PipelineCommands
{
    public static IEnumerable<Command> Create(IServiceProvider services)
    {
        var limit = new Option<int?>("--limit") { Description = "Only the first N movies (a quicker demo database)." };
        var rebuild = new Option<bool>("--rebuild") { Description = "Drop and rebuild the vector index." };

        var migrate = new Command("migrate", "Apply database schema migrations (FluentMigrator).");
        migrate.SetAction(async (_, ct) => await Migrate(services, ct));

        var import = new Command(
            "import",
            "Fetch the pinned dataset revision, verify it, stage the raw rows, and upsert movies. Rejected rows are recorded."
        )
        {
            limit,
        };
        import.SetAction(async (parse, ct) => await Import(services, parse.GetValue(limit), ct));

        var embed = new Command(
            "embed",
            "Embed movies that are new or changed for the configured model. Resumable: stop it any time."
        )
        {
            limit,
        };
        embed.SetAction(async (parse, ct) => await Embed(services, parse.GetValue(limit), ct));

        var index = new Command(
            "index",
            "Build the HNSW index for the configured model's vectors (after a bulk embed)."
        )
        {
            rebuild,
        };
        index.SetAction(async (parse, ct) => await Index(services, parse.GetValue(rebuild), ct));

        var verify = new Command(
            "verify",
            "Check that the numbers add up: import accounted for, everything embedded, index present."
        );
        verify.SetAction(async (_, ct) => await Verify(services, ct));

        var ingest = new Command("ingest", "Run migrate, import, embed, index and verify in order.") { limit };
        ingest.SetAction(
            async (parse, ct) =>
            {
                await Migrate(services, ct);
                await Import(services, parse.GetValue(limit), ct);
                await Embed(services, null, ct);
                await Index(services, rebuild: false, ct);
                return await Verify(services, ct);
            }
        );

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
        using var scope = services.CreateScope();
        Console.WriteLine("import: fetching and verifying the pinned dataset ...");
        var result = await scope
            .ServiceProvider.GetRequiredService<ImportMoviesHandler>()
            .Handle(new ImportMovies(limit), ct);
        Console.WriteLine(
            $"import: {result.Source}@{result.Revision[..8]} (run {result.RunId}) in {result.Elapsed.TotalSeconds:N1}s"
        );
        Console.WriteLine(
            $"        staged {result.Staged:N0} rows -> {result.Imported:N0} movies ({result.New:N0} new), {result.Rejected:N0} rows rejected (see movie_rejects)"
        );
        return 0;
    }

    private static async Task<int> Embed(IServiceProvider services, int? limit, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<EmbeddingOptions>();
        var progress = new Progress<EmbedProgress>(p =>
            Console.Write(
                $"\r  {p.Embedded, 6:N0} / {p.Total:N0}  ({p.PerSecond:N0}/s, ~{(p.Total - p.Embedded) / Math.Max(p.PerSecond, 0.001):N0}s left)   "
            )
        );

        var result = await scope
            .ServiceProvider.GetRequiredService<EmbedMoviesHandler>()
            .Handle(new EmbedMovies(options.BatchSize, options.MaxConcurrency, limit), progress, ct);
        Console.WriteLine(
            result.Embedded == 0
                ? $"embed: nothing pending for {result.Model}."
                : $"\nembed: {result.Embedded:N0} of {result.Pending:N0} pending movies embedded with {result.Model} in {result.Elapsed:mm\\:ss} (run {result.RunId})."
        );
        return 0;
    }

    private static async Task<int> Index(IServiceProvider services, bool rebuild, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var started = DateTime.UtcNow;
        var status = await scope
            .ServiceProvider.GetRequiredService<BuildVectorIndexHandler>()
            .Handle(new BuildVectorIndex(rebuild), ct);
        Console.WriteLine(
            $"index: {status.Name} ({status.Size}) ready in {(DateTime.UtcNow - started).TotalSeconds:N1}s."
        );
        return 0;
    }

    private static async Task<int> Verify(IServiceProvider services, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var report = await scope.ServiceProvider.GetRequiredService<VerifyIngestionHandler>().Handle(ct);
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
