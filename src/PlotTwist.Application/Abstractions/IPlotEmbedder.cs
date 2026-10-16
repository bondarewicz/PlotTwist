namespace PlotTwist.Application.Abstractions;

using Domain.Embeddings;
using Domain.Movies;
using PlotTwist.Domain.Search;

public interface IPlotEmbedder
{
    EmbeddingModel Model { get; }

    string ProviderName { get; }

    Task<IReadOnlyList<Embedding>> EmbedSearchableText(IReadOnlyList<Movie> movies, CancellationToken ct = default);

    Task<Embedding> EmbedPlotDescription(PlotDescription plot, CancellationToken ct = default);
}
