namespace PlotTwist.Application.Search;

using System.Diagnostics;
using Abstractions;
using Domain.Embeddings;

public sealed class SearchMoviesHandler(IPlotEmbedder embedder, IMovieEmbeddingRepository embeddings)
{
    public async Task<SearchMoviesResult> Handle(SearchMovies query, CancellationToken ct = default)
    {
        SearchMovies.EnsureValidTop(query.Top);

        var stopwatch = Stopwatch.StartNew();
        var embedding = await embedder.EmbedPlotDescription(query.Plot, ct);
        var embeddingTime = stopwatch.Elapsed;

        stopwatch.Restart();
        var matches = await embeddings.FindSimilar(embedding, embedder.Model, query.Criteria, query.Top, ct);
        var searchTime = stopwatch.Elapsed;

        return new SearchMoviesResult(
            query.Plot,
            query.Criteria,
            matches,
            new SearchMetadata(embedder.Model.Name, embedding.Dimensions, embeddingTime, searchTime)
        );
    }
}
