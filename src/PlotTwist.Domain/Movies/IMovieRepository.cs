namespace PlotTwist.Domain.Movies;

using PlotTwist.Domain.Embeddings;
using PlotTwist.Domain.Search;

public interface IMovieRepository
{
    // Insert or replace by id; movies must be embedded.
    Task Save(IReadOnlyCollection<Movie> movies, CancellationToken ct = default);

    Task<int> Count(CancellationToken ct = default);

    // Movies matching the criteria, closest plot first. Similarity and filtering both happen in the store.
    Task<IReadOnlyList<MovieMatch>> FindSimilar(
        Embedding plot,
        MovieCriteria criteria,
        int top,
        CancellationToken ct = default
    );

    // Every movie with its embedding, for the brute-force comparison. Fine for ~35k rows, not for millions.
    IAsyncEnumerable<Movie> ListEmbedded(CancellationToken ct = default);
}
