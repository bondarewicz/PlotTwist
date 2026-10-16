namespace PlotTwist.Evaluation.Retrieval;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

// Deterministic retrieval metric, no LLM involved: where did the expected film land in the results?
// Recorded per scenario so the report shows which descriptions regress when something changes.
public sealed class RankEvaluator : IEvaluator
{
    public const string RankMetric = "Rank";
    public const string ReciprocalRankMetric = "Reciprocal Rank";

    // A film the user has to scroll past more than this many results for counts as "not found".
    public const int AcceptableRank = 5;

    public IReadOnlyCollection<string> EvaluationMetricNames => [RankMetric, ReciprocalRankMetric];

    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default
    )
    {
        var context =
            additionalContext?.OfType<RankContext>().SingleOrDefault()
            ?? throw new InvalidOperationException($"{nameof(RankEvaluator)} needs a {nameof(RankContext)}.");

        var rank = new NumericMetric(
            RankMetric,
            context.Rank,
            $"Position of {context.Expected} in the top {context.Depth}; empty if not found."
        );
        rank.Interpretation = context.Rank is { } r and <= AcceptableRank
            ? new EvaluationMetricInterpretation(
                r == 1 ? EvaluationRating.Exceptional : EvaluationRating.Good,
                failed: false,
                $"Found at rank {r}."
            )
            : new EvaluationMetricInterpretation(
                EvaluationRating.Unacceptable,
                failed: true,
                context.Rank is { } late
                    ? $"Found only at rank {late}."
                    : $"Not in the top {context.Depth}; top result was {context.TopResult}."
            );

        var reciprocal = new NumericMetric(ReciprocalRankMetric, context.Rank is { } x ? 1.0 / x : 0);

        return ValueTask.FromResult(new EvaluationResult(rank, reciprocal));
    }
}

public sealed class RankContext(string expected, int? rank, int depth, string topResult)
    : EvaluationContext(
        "Retrieval",
        new TextContent($"Expected {expected}; rank {rank?.ToString() ?? "none"} of {depth}; top result {topResult}.")
    )
{
    public string Expected { get; } = expected;
    public int? Rank { get; } = rank;
    public int Depth { get; } = depth;
    public string TopResult { get; } = topResult;
}
