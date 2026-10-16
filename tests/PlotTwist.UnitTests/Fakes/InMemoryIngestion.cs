namespace PlotTwist.UnitTests.Fakes;

using System.Runtime.CompilerServices;
using PlotTwist.Application.Abstractions;
using PlotTwist.Application.Import;

internal sealed class InMemoryIngestion(params RawMovieRow[] rows) : IMovieSource, IMovieStaging, IIngestionRuns
{
    private long nextRun;

    public Dictionary<long, (string Kind, string Status, IngestionCounts? Counts, string? Error)> Runs { get; } = [];

    public List<MovieReject> Rejects { get; } = [];

    public Task<SourceSnapshot> Fetch(CancellationToken ct = default) =>
        Task.FromResult(new SourceSnapshot("fake", "abc123def456", "sha", "CC0", "unused"));

    public Task<int> Stage(SourceSnapshot snapshot, long runId, CancellationToken ct = default) => Task.FromResult(rows.Length);

    public async IAsyncEnumerable<RawMovieRow> Read(long runId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var row in rows)
            yield return row;
        await Task.CompletedTask;
    }

    public Task<long> Start(string kind, string details, CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref nextRun);
        Runs[id] = (kind, "running", null, null);
        return Task.FromResult(id);
    }

    public Task Complete(long runId, IngestionCounts counts, CancellationToken ct = default)
    {
        Runs[runId] = (Runs[runId].Kind, "completed", counts, null);
        return Task.CompletedTask;
    }

    public Task Fail(long runId, string error, CancellationToken ct = default)
    {
        Runs[runId] = (Runs[runId].Kind, "failed", null, error);
        return Task.CompletedTask;
    }

    public Task SaveRejects(long runId, IReadOnlyCollection<MovieReject> rejects, CancellationToken ct = default)
    {
        Rejects.AddRange(rejects);
        return Task.CompletedTask;
    }
}
