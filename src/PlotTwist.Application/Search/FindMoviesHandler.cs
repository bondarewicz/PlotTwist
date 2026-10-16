namespace PlotTwist.Application.Search;

using System.Diagnostics;
using PlotTwist.Application.Abstractions;
using PlotTwist.Domain;
using PlotTwist.Domain.Movies;

public sealed class FindMoviesHandler(IPlotEmbedder embedder, IMovieRepository movies)
{
    public async Task<FindMoviesResult> Handle(FindMovies query, CancellationToken ct = default)
    {
        if (query.Top is < 1 or > FindMovies.MaxTop)
            throw new DomainException($"Ask for between 1 and {FindMovies.MaxTop} results.");

        var stopwatch = Stopwatch.StartNew();
        var embedding = await embedder.EmbedDescription(query.Description, ct);
        var embeddingTime = stopwatch.Elapsed;

        stopwatch.Restart();
        var matches = await movies.FindSimilar(embedding, query.Criteria, query.Top, ct);
        var searchTime = stopwatch.Elapsed;

        return new FindMoviesResult(
            query.Description,
            query.Criteria,
            matches,
            new SearchMetadata(embedder.Model, embedding.Dimensions, embeddingTime, searchTime));
    }
}
