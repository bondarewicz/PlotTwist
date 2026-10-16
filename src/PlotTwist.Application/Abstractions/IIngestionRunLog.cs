namespace PlotTwist.Application.Abstractions;

using Import;

public interface IIngestionRunLog
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
    // Cancelled only when the caller's own token was signalled; any other exception, including a provider's own timeout, is a failure.
    public static async Task RecordOutcome(
        this IIngestionRunLog runs,
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
        catch { }
    }
}
