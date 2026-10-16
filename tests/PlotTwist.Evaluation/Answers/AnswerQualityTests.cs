namespace PlotTwist.Evaluation.Answers;

using Application.Assistant;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.DependencyInjection;

// End-to-end answer quality of the agent, in both retrieval modes. Slow and model-dependent (a chat model answers,
// a judge model grades), so it runs separately from the retrieval gate:
//   dotnet test --project tests/PlotTwist.Evaluation -- --filter-trait "Category=Answers"
//
// Two kinds of evaluator:
// - CitedFilmsEvaluator is deterministic: did the answer cite only films the searches returned?
// - Groundedness/Relevance are LLM-as-judge (Microsoft.Extensions.AI.Evaluation.Quality), scored 1-5 by the judge.
[Trait("Category", "Answers")]
public sealed class AnswerQualityTests(EvaluationHost host)
{
    public static TheoryData<string, AssistantMode> Questions =>
        new()
        {
            { "a 90s sci-fi film where astronauts are stranded", AssistantMode.Tool },
            { "a 90s sci-fi film where astronauts are stranded", AssistantMode.Context },
            { "an animated film about toys that come alive", AssistantMode.Tool },
            { "an animated film about toys that come alive", AssistantMode.Context },
        };

    [Theory]
    [MemberData(nameof(Questions))]
    public async Task Answer_is_grounded_in_the_search_results(string question, AssistantMode mode)
    {
        var ct = TestContext.Current.CancellationToken;
        var answer = await host
            .Services.GetRequiredService<AskAssistantHandler>()
            .Handle(new AskAssistant(question, mode), ct);

        var sources = answer.Searches.SelectMany(s => s.Result.Matches).DistinctBy(m => m.Movie.Id).ToList();
        var groundingText = string.Join(
            "\n\n",
            sources.Select(m => $"{m.Movie} [{string.Join(", ", m.Movie.Genres)}]: {m.Movie.PlotSummary}")
        );

        IEvaluator[] evaluators = [new CitedFilmsEvaluator(), new GroundednessEvaluator(), new RelevanceEvaluator()];
        var reporting = host.CreateReporting(evaluators, "answers", $"mode:{mode}", $"chat:{answer.ChatModel}");
        await using var scenario = await reporting.CreateScenarioRunAsync(
            $"Answers.{mode}.{Slug(question)}",
            cancellationToken: ct
        );
        var result = await scenario.EvaluateAsync(
            [new ChatMessage(ChatRole.User, question)],
            new ChatResponse(new ChatMessage(ChatRole.Assistant, answer.Text)),
            [
                new SourcesContext(sources.Select(m => (m.Movie.Title, m.Movie.ReleaseYear.Value)).ToList()),
                new GroundednessEvaluatorContext(groundingText),
            ],
            ct
        );

        Assert.True(answer.Searches.Count > 0, $"{mode} mode answered without searching: the answer is unverified.");
        var invented = result.Get<NumericMetric>(CitedFilmsEvaluator.InventedMetric);
        Assert.False(invented.Interpretation!.Failed, invented.Interpretation.Reason);
        // A judge that returns no parseable score says nothing about the answer: skip (visible in the run and the
        // report) rather than pass silently or fail the answer for the judge's mistake.
        var groundedness = result.Get<NumericMetric>(GroundednessEvaluator.GroundednessMetricName);
        if (groundedness.Value is null)
            Assert.Skip(
                $"The judge returned no Groundedness score: {string.Join("; ", groundedness.Diagnostics?.Select(d => d.Message.Trim()[..Math.Min(200, d.Message.Trim().Length)]) ?? [])}"
            );
        Assert.False(groundedness.Interpretation!.Failed, $"Groundedness {groundedness.Value}: {groundedness.Reason}");
    }

    private static string Slug(string text) =>
        string.Join('-', text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(6));
}
