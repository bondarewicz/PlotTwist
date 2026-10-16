namespace PlotTwist.Application.Abstractions;

using Import;

// An audit trail of every import and embed run: what went in, what came out, how long it took.
public interface IIngestionRuns
{
    Task<long> Start(string kind, string details, CancellationToken ct = default);

    Task Complete(long runId, IngestionCounts counts, CancellationToken ct = default);

    Task Fail(long runId, string error, IngestionCounts counts, CancellationToken ct = default);

    Task Cancel(long runId, IngestionCounts counts, CancellationToken ct = default);

    Task SaveRejects(long runId, IReadOnlyCollection<MovieReject> rejects, CancellationToken ct = default);
}

public sealed record IngestionCounts(int Read = 0, int Loaded = 0, int Rejected = 0, int Embedded = 0);

public static class IngestionRunOutcome
{
    // A run the caller cancelled (ct.IsCancellationRequested) is recorded as cancelled; anything else - including
    // a provider's own TaskCanceledException timeout - is a failure with its real message. Best-effort: recording
    // the outcome runs with CancellationToken.None and never throws, so the original exception always propagates.
    public static async Task RecordOutcome(
        this IIngestionRuns runs,
        long runId,
        Exception error,
        IngestionCounts counts,
        CancellationToken ct
    )
    {
        try
        {
            if (error is OperationCanceledException && ct.IsCancellationRequested)
                await runs.Cancel(runId, counts, CancellationToken.None);
            else
                await runs.Fail(runId, error.Message, counts, CancellationToken.None);
        }
        catch
        {
            // The caller's exception (recorded above, or not) still propagates; losing the audit write is secondary.
        }
    }
}
