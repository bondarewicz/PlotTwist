namespace PlotTwist.Application.Abstractions;

using PlotTwist.Application.Import;

// An audit trail of every import and embed run: what went in, what came out, how long it took.
public interface IIngestionRuns
{
    Task<long> Start(string kind, string details, CancellationToken ct = default);

    Task Complete(long runId, IngestionCounts counts, CancellationToken ct = default);

    Task Fail(long runId, string error, CancellationToken ct = default);

    Task SaveRejects(long runId, IReadOnlyCollection<MovieReject> rejects, CancellationToken ct = default);
}

public sealed record IngestionCounts(int Read = 0, int Loaded = 0, int Rejected = 0, int Embedded = 0);
