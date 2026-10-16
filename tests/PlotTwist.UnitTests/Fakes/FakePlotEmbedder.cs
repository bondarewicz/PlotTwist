namespace PlotTwist.UnitTests.Fakes;

using PlotTwist.Application.Abstractions;
using PlotTwist.Domain.Embeddings;
using PlotTwist.Domain.Movies;
using PlotTwist.Domain.Search;

internal sealed class FakePlotEmbedder(
    int? failOnCall = null,
    int? cancelAfterCall = null,
    CancellationTokenSource? cancelSource = null,
    Exception? failWith = null
) : IPlotEmbedder
{
    private int calls;

    public EmbeddingModel Model { get; } = EmbeddingModel.Create("fake-embedder", 2);

    public string ProviderName => "fake";

    public List<int> BatchSizes { get; } = [];

    public Task<IReadOnlyList<Embedding>> EmbedSearchableText(
        IReadOnlyList<Movie> movies,
        CancellationToken ct = default
    )
    {
        var call = Interlocked.Increment(ref calls);
        if (call == cancelAfterCall)
        {
            cancelSource!.Cancel();
            ct.ThrowIfCancellationRequested();
        }
        if (call == failOnCall)
            throw failWith ?? new HttpRequestException("model went away");

        lock (BatchSizes)
            BatchSizes.Add(movies.Count);
        return Task.FromResult<IReadOnlyList<Embedding>>(movies.Select(m => Embed(m.SearchableText)).ToList());
    }

    public Task<Embedding> EmbedPlotDescription(PlotDescription plot, CancellationToken ct = default) =>
        Task.FromResult(Embed(plot.Value));

    private static Embedding Embed(string text) => new([text.Length, 1f]);
}
