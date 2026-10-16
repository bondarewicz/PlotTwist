namespace PlotTwist.Application.Abstractions;

using Domain.Embeddings;
using Domain.Movies;
using PlotTwist.Domain.Search;

public interface IPlotEmbedder
{
    EmbeddingModel Model { get; }

    string ProviderName { get; }

    Task<IReadOnlyList<Embedding>> EmbedMovies(IReadOnlyList<Movie> movies, CancellationToken ct = default);

    Task<Embedding> EmbedDescription(PlotDescription description, CancellationToken ct = default);
}
