namespace PlotTwist.Domain.Embeddings;

using PlotTwist.Domain.Movies;
using PlotTwist.Domain.Search;

public interface IMovieEmbeddingRepository
{
    // Movies with no embedding in this model, or whose SearchableText changed since they were embedded.
    // This is the embed job's work queue: re-running after a crash continues where it stopped.
    Task<IReadOnlyList<MovieId>> ListPending(EmbeddingModel model, CancellationToken ct = default);

    Task Save(IReadOnlyCollection<MovieEmbedding> embeddings, CancellationToken ct = default);

    Task<int> Count(EmbeddingModel model, CancellationToken ct = default);

    // Movies matching the criteria, closest plot first. Similarity and filtering both happen in the store.
    Task<IReadOnlyList<MovieMatch>> FindSimilar(
        Embedding plot,
        EmbeddingModel model,
        MovieCriteria criteria,
        int top,
        CancellationToken ct = default
    );

    // Every movie with its embedding in this model, for the brute-force comparison. Fine for ~35k rows, not millions.
    IAsyncEnumerable<(Movie Movie, Embedding Vector)> ListAll(EmbeddingModel model, CancellationToken ct = default);
}
