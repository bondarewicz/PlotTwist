namespace PlotTwist.Evaluation.Retrieval;

using System.Globalization;
using Application.Search;
using Domain.Search;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.DependencyInjection;

// Retrieval quality of the search alone (embedding model + Movie.SearchableText + pgvector), no chat model.
// Fast and deterministic: run it after every change to embeddings, prefixes, the embedded text or index settings.
//
// Every case is recorded as its own scenario (see `dotnet aieval report`), but the build gates on the aggregate:
// a benchmark always has hard cases, and one regressing case shouldn't hide behind - or be confused with - a
// regression across the board.
[Trait("Category", "Retrieval")]
public sealed class RetrievalTests(EvaluationHost host)
{
    private const int Depth = 10;

    // Ratchet: set to the current baseline, raise it when retrieval improves, never lower it to make a change pass.
    private const double MinimumRecallAt5 = 0.60;

    [Fact]
    public async Task Retrieval_meets_the_quality_gate()
    {
        var ct = TestContext.Current.CancellationToken;
        var reporting = host.CreateReporting([new RankEvaluator()], "retrieval");
        var outcomes = new List<(RetrievalCase Case, int? Rank, string Top)>();

        foreach (var c in RetrievalCases.All)
        {
            var (rank, top) = await Search(c, ct);
            outcomes.Add((c, rank, top));

            await using var scenario = await reporting.CreateScenarioRunAsync(
                $"Retrieval.{c.Name}",
                cancellationToken: ct
            );
            await scenario.EvaluateAsync(
                [new ChatMessage(ChatRole.User, c.Description)],
                new ChatResponse(new ChatMessage(ChatRole.Assistant, top)),
                [new RankContext($"{c.ExpectedTitle} ({c.ExpectedYear})", rank, Depth, top)],
                ct
            );
        }

        var recallAt1 = outcomes.Count(o => o.Rank == 1) / (double)outcomes.Count;
        var recallAt5 = outcomes.Count(o => o.Rank <= RankEvaluator.AcceptableRank) / (double)outcomes.Count;
        var mrr = outcomes.Average(o => o.Rank is { } r ? 1.0 / r : 0);
        var summary = string.Create(
            CultureInfo.InvariantCulture,
            $"recall@1 {recallAt1:P0}, recall@5 {recallAt5:P0}, MRR {mrr:F3} over {outcomes.Count} cases"
        );
        var misses = outcomes
            .Where(o => o.Rank is not <= RankEvaluator.AcceptableRank)
            .Select(o => $"  {o.Case.Name}: rank {o.Rank?.ToString() ?? $">{Depth}"}, top result {o.Top}");

        TestContext.Current.SendDiagnosticMessage(
            summary + Environment.NewLine + string.Join(Environment.NewLine, misses)
        );
        Assert.True(
            recallAt5 >= MinimumRecallAt5,
            $"{summary}; gate is recall@5 >= {MinimumRecallAt5:P0}.{Environment.NewLine}{string.Join(Environment.NewLine, misses)}"
        );
    }

    private async Task<(int? Rank, string Top)> Search(RetrievalCase c, CancellationToken ct)
    {
        var result = await host
            .Services.GetRequiredService<FindMoviesHandler>()
            .Handle(new FindMovies(PlotDescription.Create(c.Description), c.Criteria ?? MovieCriteria.None, Depth), ct);

        var index = result
            .Matches.ToList()
            .FindIndex(m => m.Movie.Title == c.ExpectedTitle && m.Movie.ReleaseYear.Value == c.ExpectedYear);
        return (index < 0 ? null : index + 1, result.Matches.FirstOrDefault()?.Movie.ToString() ?? "nothing");
    }
}
