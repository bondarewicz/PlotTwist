namespace PlotTwist.UnitTests.Fakes;

using PlotTwist.Application.Abstractions;
using PlotTwist.Domain.Embeddings;
using PlotTwist.Domain.Movies;
using PlotTwist.Domain.Search;

// Deterministic stand-in: a two-dimensional vector derived from the text length. Can be told to fail after N calls.
internal sealed class FakePlotEmbedder(int? failOnCall = null) : IPlotEmbedder
{
    private int calls;

    public EmbeddingModel Model { get; } = EmbeddingModel.Create("fake-embedder", 2);

    public List<int> BatchSizes { get; } = [];

    public Task<IReadOnlyList<Embedding>> EmbedMovies(IReadOnlyList<Movie> movies, CancellationToken ct = default)
    {
        if (++calls == failOnCall)
            throw new HttpRequestException("model went away");

        lock (BatchSizes)
            BatchSizes.Add(movies.Count);
        return Task.FromResult<IReadOnlyList<Embedding>>(movies.Select(m => Embed(m.SearchableText)).ToList());
    }

    public Task<Embedding> EmbedDescription(PlotDescription description, CancellationToken ct = default) =>
        Task.FromResult(Embed(description.Value));

    private static Embedding Embed(string text) => new([text.Length, 1f]);
}
