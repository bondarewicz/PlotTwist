namespace PlotTwist.Cli.Commands;

using System.CommandLine;
using Application.Search;
using Domain.Search;
using Microsoft.Extensions.DependencyInjection;

internal static class SearchCommands
{
    private static readonly Argument<string> Plot = new("plot") { Description = "What happens in the film." };
    private static readonly Option<int?> From = new("--from") { Description = "Earliest release year." };
    private static readonly Option<int?> To = new("--to") { Description = "Latest release year." };
    private static readonly Option<string?> Genre = new("--genre")
    {
        Description = "One genre, e.g. drama, sci-fi, animated.",
    };
    private static readonly Option<int> Top = new("--top")
    {
        Description = "Number of results.",
        DefaultValueFactory = _ => FindMovies.DefaultTop,
    };

    public static Command CreateSearch(IServiceProvider services)
    {
        var command = new Command("search", "Semantic search with optional year and genre filters.")
        {
            Plot,
            From,
            To,
            Genre,
            Top,
        };
        command.SetAction(
            async (parse, ct) =>
            {
                var result = await services
                    .GetRequiredService<FindMoviesHandler>()
                    .Handle(new FindMovies(Description(parse), Criteria(parse), parse.GetValue(Top)), ct);

                Console.WriteLine($"\"{result.Description}\"  where {result.Criteria}");
                Print(result.Matches);
                Console.WriteLine(
                    $"\n{result.Metadata.EmbeddingModel}, {result.Metadata.Dimensions} dims: embed {result.Metadata.EmbeddingTime.TotalMilliseconds:N0} ms + search {result.Metadata.SearchTime.TotalMilliseconds:N0} ms"
                        + (
                            MatchConfidence.IsAmbiguous(result.Matches)
                                ? $"; ambiguous (top two within {MatchConfidence.AmbiguousMargin})"
                                : ""
                        )
                );
            }
        );
        return command;
    }

    public static Command CreateCompare(IServiceProvider services)
    {
        var command = new Command(
            "compare",
            "The same search brute force in memory, with the pgvector index, and with index + filters."
        )
        {
            Plot,
            From,
            To,
            Genre,
            Top,
        };
        command.SetAction(
            async (parse, ct) =>
            {
                var comparison = await services
                    .GetRequiredService<CompareSearchStrategiesHandler>()
                    .Handle(new CompareSearchStrategies(Description(parse), Criteria(parse), parse.GetValue(Top)), ct);

                Console.WriteLine($"\"{comparison.Description}\"");
                foreach (var strategy in comparison.Strategies)
                {
                    Console.WriteLine(
                        $"\n{strategy.Strategy}  [{strategy.Elapsed.TotalMilliseconds:N0} ms]  {strategy.Notes}"
                    );
                    Print(strategy.Matches);
                }
            }
        );
        return command;
    }

    private static PlotDescription Description(ParseResult parse) => PlotDescription.Create(parse.GetValue(Plot));

    private static MovieCriteria Criteria(ParseResult parse) =>
        MovieCriteria.Create(parse.GetValue(From), parse.GetValue(To), parse.GetValue(Genre));

    private static void Print(IReadOnlyList<MovieMatch> matches)
    {
        if (matches.Count == 0)
            Console.WriteLine("   (no matches)");
        for (var i = 0; i < matches.Count; i++)
            Console.WriteLine(
                $"   {i + 1}. {matches[i].Similarity:F3}  {matches[i].Movie} [{string.Join(", ", matches[i].Movie.Genres)}]"
            );
    }
}
