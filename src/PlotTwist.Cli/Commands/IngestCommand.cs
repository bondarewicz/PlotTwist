namespace PlotTwist.Cli.Commands;

using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PlotTwist.Application.Ingest;

internal static class IngestCommand
{
    public static Command Create(IServiceProvider services)
    {
        var limit = new Option<int?>("--limit") { Description = "Only the first N films (a quicker demo set)." };
        var origin = new Option<string?>("--origin") { Description = "Only films of one origin, e.g. American." };

        var command = new Command("ingest", "Download the dataset, embed every film and upsert it into pgvector. Safe to re-run.")
        {
            limit,
            origin,
        };

        command.SetAction(async (parse, ct) =>
        {
            var progress = new Progress<IngestProgress>(p =>
                Console.Write($"\r  {p.Embedded,6:N0} / {p.Total:N0}  ({p.PerSecond:N0}/s)   "));

            var result = await services.GetRequiredService<IngestMoviesHandler>()
                .Handle(new IngestMovies(parse.GetValue(limit), parse.GetValue(origin)), progress, ct);

            Console.WriteLine($"\nEmbedded {result.Embedded:N0} films from {result.Dataset} with {result.EmbeddingModel} in {result.Elapsed:mm\\:ss}.");
        });

        return command;
    }
}
