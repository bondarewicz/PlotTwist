namespace PlotTwist.Evaluation.Answers;

using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

public sealed partial class CitedMoviesEvaluator : IEvaluator
{
    public const string InventedMetric = "Invented Movies";
    public const string CitedMetric = "Cited Movies";

    public IReadOnlyCollection<string> EvaluationMetricNames => [InventedMetric, CitedMetric];

    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default
    )
    {
        var citedMovies =
            additionalContext?.OfType<CitedMoviesContext>().SingleOrDefault()
            ?? throw new InvalidOperationException(
                $"{nameof(CitedMoviesEvaluator)} needs a {nameof(CitedMoviesContext)}."
            );

        var cited = Citation()
            .Matches(modelResponse.Text)
            .Select(m =>
                (Title: m.Groups["title"].Value.Trim(' ', '*', '"', '_'), Year: int.Parse(m.Groups["year"].Value))
            )
            .Distinct()
            .ToList();
        var invented = cited
            .Where(c =>
                !citedMovies.Movies.Any(s =>
                    s.Year == c.Year && c.Title.EndsWith(s.Title, StringComparison.OrdinalIgnoreCase)
                )
            )
            .ToList();

        var inventedMetric = new NumericMetric(InventedMetric, invented.Count, "Cited movies that no search returned.");
        inventedMetric.Interpretation =
            invented.Count == 0
                ? new EvaluationMetricInterpretation(
                    EvaluationRating.Exceptional,
                    failed: false,
                    "Every cited movie came from the searches."
                )
                : new EvaluationMetricInterpretation(
                    EvaluationRating.Unacceptable,
                    failed: true,
                    "Not in the search results: " + string.Join(", ", invented.Select(i => $"{i.Title} ({i.Year})"))
                );

        var citedMetric = new NumericMetric(CitedMetric, cited.Count, "Movies cited as \"Title (Year)\".");
        if (cited.Count == 0)
            citedMetric.Interpretation = new EvaluationMetricInterpretation(
                EvaluationRating.Poor,
                failed: true,
                "The answer cites no movies in the requested format."
            );

        return ValueTask.FromResult(new EvaluationResult(inventedMetric, citedMetric));
    }

    [GeneratedRegex(@"(?<title>[A-Z0-9][^()\n*\[\]]{0,80}?)\s*\((?<year>(18|19|20)\d{2})\)")]
    private static partial Regex Citation();
}

public sealed class CitedMoviesContext(IReadOnlyList<(string Title, int Year)> movies)
    : EvaluationContext("Citations", new TextContent(string.Join("\n", movies.Select(f => $"{f.Title} ({f.Year})"))))
{
    public IReadOnlyList<(string Title, int Year)> Movies { get; } = movies;
}
