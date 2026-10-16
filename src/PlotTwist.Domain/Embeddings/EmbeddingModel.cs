namespace PlotTwist.Domain.Embeddings;

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
            throw new DomainException("An embedding model needs a name.");

        if (dimensions is < 1 or > MaxDimensions)
            throw new DomainException($"Dimensions must be 1-{MaxDimensions}, not {dimensions}.");

        return new EmbeddingModel(name.Trim(), dimensions);
    }

    public override string ToString() => $"{Name} ({Dimensions} dims)";
}
