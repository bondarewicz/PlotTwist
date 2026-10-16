namespace PlotTwist.Application.Abstractions;

using PlotTwist.Domain.Embeddings;
using PlotTwist.Domain.Movies;
using PlotTwist.Domain.Search;

// Turns text into vectors. Movies and search descriptions may be embedded differently
// (nomic-embed-text uses separate task prefixes), so they are separate operations.
public interface IPlotEmbedder
{
    string Model { get; }

    Task<IReadOnlyList<Embedding>> EmbedMovies(IReadOnlyList<Movie> movies, CancellationToken ct = default);

    Task<Embedding> EmbedDescription(PlotDescription description, CancellationToken ct = default);
}
