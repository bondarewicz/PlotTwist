namespace PlotTwist.Domain.Embeddings;

// A text embedding vector. Immutable: the values are copied in and only exposed read-only.
public sealed class Embedding
{
    private readonly float[] values;

    public Embedding(ReadOnlySpan<float> values)
    {
        if (values.IsEmpty)
            throw new ArgumentException("Embedding must have at least one dimension.", nameof(values));

        this.values = values.ToArray();
    }

    public int Dimensions => values.Length;

    public ReadOnlyMemory<float> Values => values;
}
