namespace PlotTwist.Application.Search;

using System.Diagnostics;
using Abstractions;
using Domain;
using Domain.Embeddings;

public sealed class FindMoviesHandler(IPlotEmbedder embedder, IMovieEmbeddingRepository embeddings)
{
    public async Task<FindMoviesResult> Handle(FindMovies query, CancellationToken ct = default)
    {
        if (query.Top is < 1 or > FindMovies.MaxTop)
            throw new DomainException($"Ask for between 1 and {FindMovies.MaxTop} results.");

        var stopwatch = Stopwatch.StartNew();
        var embedding = await embedder.EmbedDescription(query.Description, ct);
        var embeddingTime = stopwatch.Elapsed;

        stopwatch.Restart();
        var matches = await embeddings.FindSimilar(embedding, embedder.Model, query.Criteria, query.Top, ct);
        var searchTime = stopwatch.Elapsed;

        return new FindMoviesResult(
            query.Description,
            query.Criteria,
            matches,
            new SearchMetadata(embedder.Model.Name, embedding.Dimensions, embeddingTime, searchTime)
        );
    }
}
