namespace PlotTwist.Domain.Embeddings;

using PlotTwist.Domain.Movies;

// A movie's SearchableText in one model's vector space. TextHash records which text was embedded,
// so a later change to the movie makes this embedding stale rather than silently wrong.
public sealed record MovieEmbedding
{
    public MovieEmbedding(MovieId movieId, EmbeddingModel model, string textHash, Embedding vector)
    {
        if (vector.Dimensions != model.Dimensions)
            throw new ArgumentException($"{model} cannot hold a {vector.Dimensions}-dimension vector.", nameof(vector));

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
