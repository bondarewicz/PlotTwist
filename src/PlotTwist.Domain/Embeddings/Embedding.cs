namespace PlotTwist.Domain.Embeddings;

public sealed class Embedding
{
    private readonly float[] values;

    public Embedding(ReadOnlySpan<float> values)
    {
        if (values.IsEmpty)
            throw new DomainException("Embedding must have at least one dimension.");

        this.values = values.ToArray();
    }

    public int Dimensions => values.Length;

    public ReadOnlyMemory<float> Values => values;
}
