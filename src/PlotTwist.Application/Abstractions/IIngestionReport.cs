namespace PlotTwist.Application.Abstractions;

using Domain.Embeddings;

public interface IIngestionReport
{
    Task<IngestionStatus> Get(EmbeddingModel model, CancellationToken ct = default);
}

public sealed record IngestionStatus(LastImport? LastImport, int Movies, int Embedded, int Pending);

public sealed record LastImport(
    long RunId,
    string Details,
    int Read,
    int Loaded,
    int Rejected,
    DateTimeOffset FinishedAt
);
