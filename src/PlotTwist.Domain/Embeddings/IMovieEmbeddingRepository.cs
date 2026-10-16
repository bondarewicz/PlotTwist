namespace PlotTwist.Domain.Embeddings;

using Movies;
using Search;

public interface IMovieEmbeddingRepository
{
    Task<IReadOnlyList<MovieId>> ListPending(EmbeddingModel model, CancellationToken ct = default);

    Task Save(IReadOnlyCollection<MovieEmbedding> embeddings, CancellationToken ct = default);

    Task<int> Count(EmbeddingModel model, CancellationToken ct = default);

    Task<IReadOnlyList<MovieMatch>> FindSimilar(
        Embedding plot,
        EmbeddingModel model,
        MovieCriteria criteria,
        int top,
        CancellationToken ct = default
    );
}
