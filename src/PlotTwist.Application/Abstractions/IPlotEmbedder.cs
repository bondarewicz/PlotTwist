namespace PlotTwist.Application.Abstractions;

using Domain.Embeddings;
using Domain.Movies;
using PlotTwist.Domain.Search;

// Turns text into vectors in one model's space. Movies and search descriptions may be embedded differently
// (nomic-embed-text uses separate task prefixes), so they are separate operations.
public interface IPlotEmbedder
{
    EmbeddingModel Model { get; }

    string ProviderName { get; }

    Task<IReadOnlyList<Embedding>> EmbedMovies(IReadOnlyList<Movie> movies, CancellationToken ct = default);

    Task<Embedding> EmbedDescription(PlotDescription description, CancellationToken ct = default);
}
