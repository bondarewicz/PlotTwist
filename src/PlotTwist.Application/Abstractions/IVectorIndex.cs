namespace PlotTwist.Application.Abstractions;

using Domain.Embeddings;

public interface IVectorIndex
{
    Task<VectorIndexStatus> Status(EmbeddingModel model, CancellationToken ct = default);

    Task<VectorIndexStatus> Build(EmbeddingModel model, bool rebuild, CancellationToken ct = default);
}

public sealed record VectorIndexStatus(string Name, bool Exists, string? Size);
