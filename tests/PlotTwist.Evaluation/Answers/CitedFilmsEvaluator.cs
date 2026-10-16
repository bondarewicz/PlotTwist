namespace PlotTwist.Evaluation.Answers;

using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

public sealed partial class CitedFilmsEvaluator : IEvaluator
{
    public const string InventedMetric = "Invented Films";
    public const string CitedMetric = "Cited Films";

    public IReadOnlyCollection<string> EvaluationMetricNames => [InventedMetric, CitedMetric];

    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default
    )
    {
        var sources =
            additionalContext?.OfType<SourcesContext>().SingleOrDefault()
            ?? throw new InvalidOperationException($"{nameof(CitedFilmsEvaluator)} needs a {nameof(SourcesContext)}.");

        var cited = Citation()
            .Matches(modelResponse.Text)
            .Select(m =>
                (Title: m.Groups["title"].Value.Trim(' ', '*', '"', '_'), Year: int.Parse(m.Groups["year"].Value))
            )
            .Distinct()
            .ToList();
        var invented = cited
            .Where(c =>
                !sources.Films.Any(s =>
                    s.Year == c.Year && c.Title.EndsWith(s.Title, StringComparison.OrdinalIgnoreCase)
                )
            )
            .ToList();

        var inventedMetric = new NumericMetric(InventedMetric, invented.Count, "Cited films that no search returned.");
        inventedMetric.Interpretation =
            invented.Count == 0
                ? new EvaluationMetricInterpretation(
                    EvaluationRating.Exceptional,
                    failed: false,
                    "Every cited film came from the searches."
                )
                : new EvaluationMetricInterpretation(
                    EvaluationRating.Unacceptable,
                    failed: true,
                    "Not in the search results: " + string.Join(", ", invented.Select(i => $"{i.Title} ({i.Year})"))
                );

        var citedMetric = new NumericMetric(CitedMetric, cited.Count, "Films cited as \"Title (Year)\".");
        if (cited.Count == 0)
            citedMetric.Interpretation = new EvaluationMetricInterpretation(
                EvaluationRating.Poor,
                failed: true,
                "The answer cites no films in the requested format."
            );

        return ValueTask.FromResult(new EvaluationResult(inventedMetric, citedMetric));
    }

    [GeneratedRegex(@"(?<title>[A-Z0-9][^()\n*\[\]]{0,80}?)\s*\((?<year>(18|19|20)\d{2})\)")]
    private static partial Regex Citation();
}

public sealed class SourcesContext(IReadOnlyList<(string Title, int Year)> films)
    : EvaluationContext("Sources", new TextContent(string.Join("\n", films.Select(f => $"{f.Title} ({f.Year})"))))
{
    public IReadOnlyList<(string Title, int Year)> Films { get; } = films;
}
