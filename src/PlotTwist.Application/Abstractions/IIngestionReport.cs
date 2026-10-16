namespace PlotTwist.Application.Abstractions;

using Domain.Embeddings;

// Read side for verification: the numbers that must add up after an import and embed.
public interface IIngestionReport
{
    Task<IngestionSnapshot> Get(EmbeddingModel model, CancellationToken ct = default);
}

public sealed record IngestionSnapshot(LastImport? LastImport, int Movies, int Embedded, int Pending);

public sealed record LastImport(
    long RunId,
    string Details,
    int Read,
    int Loaded,
    int Rejected,
    DateTimeOffset FinishedAt
);
