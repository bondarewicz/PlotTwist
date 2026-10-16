namespace PlotTwist.Domain.Embeddings;

using Movies;

public sealed record MovieEmbedding
{
    public MovieEmbedding(MovieId movieId, EmbeddingModel model, string textHash, Embedding vector)
    {
        if (vector.Dimensions != model.Dimensions)
            throw new DomainException($"{model} cannot hold a {vector.Dimensions}-dimension vector.");

        MovieId = movieId;
        Model = model;
        TextHash = textHash;
        Vector = vector;
    }

    public MovieId MovieId { get; }

    public EmbeddingModel Model { get; }

    public string TextHash { get; }

    public Embedding Vector { get; }
}
