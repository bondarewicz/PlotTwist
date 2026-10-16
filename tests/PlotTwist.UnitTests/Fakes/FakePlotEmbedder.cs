namespace PlotTwist.UnitTests.Fakes;

using PlotTwist.Application.Abstractions;
using PlotTwist.Domain.Embeddings;
using PlotTwist.Domain.Movies;
using PlotTwist.Domain.Search;

// Deterministic stand-in: a two-dimensional vector derived from the text length. Enough to test use cases.
internal sealed class FakePlotEmbedder : IPlotEmbedder
{
    public string Model => "fake-embedder";

    public List<int> BatchSizes { get; } = [];

    public Task<IReadOnlyList<Embedding>> EmbedMovies(IReadOnlyList<Movie> movies, CancellationToken ct = default)
    {
        BatchSizes.Add(movies.Count);
        return Task.FromResult<IReadOnlyList<Embedding>>(movies.Select(m => Embed(m.SearchableText)).ToList());
    }

    public Task<Embedding> EmbedDescription(PlotDescription description, CancellationToken ct = default) =>
        Task.FromResult(Embed(description.Value));

    private static Embedding Embed(string text) => new([text.Length, 1f]);
}
