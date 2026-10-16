namespace PlotTwist.Evaluation.Answers;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

[Trait("Category", "Evaluators")]
public sealed class CitedMoviesEvaluatorTests
{
    private static readonly CitedMoviesContext Movies = new([
        ("Toy Story", 1995),
        ("Toy Story 2", 1999),
        ("Apollo 13", 1995),
    ]);

    [Theory]
    [InlineData("Try **Toy Story (1995)** and Apollo 13 (1995).")]
    [InlineData("[SourceDocName: Toy Story 2 (1999)](https://en.wikipedia.org/wiki/Toy_Story_2)")]
    [InlineData("*Toy Story (1995)*: toys come alive.")]
    [InlineData("[Source: Toy Story (1995)](https://en.wikipedia.org/wiki/Toy_Story)")]
    [InlineData("I'd suggest Apollo 13 (1995).")]
    public async Task Citations_of_searched_movies_are_not_invented(string answer) =>
        Assert.Equal(0, (await Evaluate(answer)).Get<NumericMetric>(CitedMoviesEvaluator.InventedMetric).Value);

    [Fact]
    public async Task A_movie_no_search_returned_is_invented()
    {
        var metric = (await Evaluate("Toy Story (1995) and Small Soldiers (1998).")).Get<NumericMetric>(
            CitedMoviesEvaluator.InventedMetric
        );

        Assert.Equal(1, metric.Value);
        Assert.Contains("Small Soldiers (1998)", metric.Interpretation!.Reason);
    }

    [Fact]
    public async Task An_answer_without_citations_fails_the_citation_format() =>
        Assert.True(
            (await Evaluate("Nothing matched."))
                .Get<NumericMetric>(CitedMoviesEvaluator.CitedMetric)
                .Interpretation!.Failed
        );

    private static async Task<EvaluationResult> Evaluate(string answer) =>
        await new CitedMoviesEvaluator().EvaluateAsync(
            [new ChatMessage(ChatRole.User, "toys")],
            new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)),
            additionalContext: [Movies],
            cancellationToken: TestContext.Current.CancellationToken
        );
}
