namespace PlotTwist.Application.Abstractions;

using PlotTwist.Domain.Embeddings;

// The approximate-nearest-neighbour index for one model's vectors. Built after a bulk load (much faster than
// maintaining it row by row during the load); afterwards new embeddings are added to it incrementally.
public interface IVectorIndex
{
    Task<VectorIndexStatus> Status(EmbeddingModel model, CancellationToken ct = default);

    Task<VectorIndexStatus> Build(EmbeddingModel model, bool rebuild, CancellationToken ct = default);
}

public sealed record VectorIndexStatus(string Name, bool Exists, string? Size);
