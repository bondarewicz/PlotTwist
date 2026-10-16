namespace PlotTwist.Domain.Embeddings;

// The vector space an embedding belongs to. Vectors from different models (or dimensions) can't be compared,
// so every stored embedding and every search names its model.
public sealed record EmbeddingModel
{
    private const int MaxDimensions = 16000;

    private EmbeddingModel(string name, int dimensions)
    {
        Name = name;
        Dimensions = dimensions;
    }

    public string Name { get; }

    public int Dimensions { get; }

    public static EmbeddingModel Create(string? name, int dimensions)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("An embedding model needs a name.", nameof(name));

        if (dimensions is < 1 or > MaxDimensions)
            throw new ArgumentOutOfRangeException(
                nameof(dimensions),
                dimensions,
                $"Dimensions must be 1-{MaxDimensions}."
            );

        return new EmbeddingModel(name.Trim(), dimensions);
    }

    public override string ToString() => $"{Name} ({Dimensions} dims)";
}
