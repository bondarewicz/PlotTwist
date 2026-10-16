namespace PlotTwist.UnitTests.Application;

using System.Runtime.CompilerServices;
using Fakes;
using PlotTwist.Application.Abstractions;
using PlotTwist.Application.Import;
using PlotTwist.Domain.Movies;
using static Movies;

public sealed class ImportMoviesHandlerTests
{
    private readonly InMemoryMovieStore store = new();

    [Fact]
    public async Task Imports_movies_records_rejects_and_completes_the_run()
    {
        var ingestion = new InMemoryIngestion(
            Row(1, "Alien", "1979", "https://en.wikipedia.org/wiki/Alien"),
            Row(2, "Jaws", "1975", "https://en.wikipedia.org/wiki/Jaws"),
            Row(3, "Broken", "nope", "https://en.wikipedia.org/wiki/Broken")
        );

        var result = await Handler(ingestion).Handle(new ImportMovies(), TestContext.Current.CancellationToken);

        Assert.Equal((3, 2, 2, 1), (result.Staged, result.Imported, result.New, result.Rejected));
        Assert.Equal("Broken", Assert.Single(ingestion.Rejects).Title);
        Assert.Equal(("import", "completed"), (ingestion.Runs[result.RunId].Kind, ingestion.Runs[result.RunId].Status));
    }

    [Fact]
    public async Task Re_importing_the_same_source_creates_nothing_new()
    {
        var ingestion = new InMemoryIngestion(Row(1, "Alien", "1979", "https://en.wikipedia.org/wiki/Alien"));

        await Handler(ingestion).Handle(new ImportMovies(), TestContext.Current.CancellationToken);
        var second = await Handler(ingestion).Handle(new ImportMovies(), TestContext.Current.CancellationToken);

        Assert.Equal(0, second.New);
        Assert.Equal(1, await store.Count(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Limit_keeps_the_first_movies_only()
    {
        var ingestion = new InMemoryIngestion(
            Row(1, "A", "1990", "https://en.wikipedia.org/wiki/A"),
            Row(2, "B", "1991", "https://en.wikipedia.org/wiki/B"),
            Row(3, "C", "1992", "https://en.wikipedia.org/wiki/C")
        );

        var result = await Handler(ingestion).Handle(new ImportMovies(Limit: 2), TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Imported);
    }

    [Fact]
    public async Task A_cancelled_import_is_recorded_as_cancelled_with_its_partial_counts()
    {
        using var cancelled = new CancellationTokenSource();
        var ingestion = new InMemoryIngestion(
            Row(1, "A", "1990", "https://en.wikipedia.org/wiki/A"),
            Row(2, "B", "1991", "https://en.wikipedia.org/wiki/B")
        );
        var staging = new CancellingStaging(ingestion, cancelled, cancelAfterRow: 1);
        var handler = new ImportMoviesHandler(ingestion, staging, store, ingestion);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.Handle(new ImportMovies(), cancelled.Token)
        );

        Assert.Contains(
            ingestion.Runs.Values,
            r => r is { Kind: "import", Status: "cancelled", Counts.Read: 2, Counts.Loaded: 0, Counts.Rejected: 0 }
        );
    }

    [Fact]
    public async Task Recording_the_failure_never_masks_the_original_exception()
    {
        var ingestion = new InMemoryIngestion(Row(1, "A", "1990", "https://en.wikipedia.org/wiki/A"));
        var runs = new ThrowingOnFail(ingestion);
        var handler = new ImportMoviesHandler(ingestion, ingestion, new ThrowingRepository(), runs);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ImportMovies(), TestContext.Current.CancellationToken)
        );

        Assert.Equal("upsert exploded", error.Message);
    }

    private ImportMoviesHandler Handler(InMemoryIngestion ingestion) => new(ingestion, ingestion, store, ingestion);

    private sealed class CancellingStaging(
        IMovieStaging inner,
        CancellationTokenSource cancelSource,
        int cancelAfterRow
    ) : IMovieStaging
    {
        public Task<int> Stage(SourceSnapshot snapshot, long runId, CancellationToken ct = default) =>
            inner.Stage(snapshot, runId, ct);

        public async IAsyncEnumerable<RawMovieRow> Read(
            long runId,
            [EnumeratorCancellation] CancellationToken ct = default
        )
        {
            var n = 0;
            await foreach (var row in inner.Read(runId, ct))
            {
                if (++n == cancelAfterRow)
                {
                    cancelSource.Cancel();
                    ct.ThrowIfCancellationRequested();
                }
                yield return row;
            }
        }
    }

    private sealed class ThrowingOnFail(IIngestionRunLog inner) : IIngestionRunLog
    {
        public Task<long> Start(string kind, string details, CancellationToken ct = default) =>
            inner.Start(kind, details, ct);

        public Task Complete(long runId, IngestionCounts counts, CancellationToken ct = default) =>
            inner.Complete(runId, counts, ct);

        public Task Fail(long runId, string error, IngestionCounts counts, CancellationToken ct = default) =>
            throw new TimeoutException("db down");

        public Task Cancel(long runId, IngestionCounts counts, CancellationToken ct = default) =>
            throw new TimeoutException("db down");

        public Task SaveRejects(long runId, IReadOnlyCollection<MovieReject> rejects, CancellationToken ct = default) =>
            inner.SaveRejects(runId, rejects, ct);
    }

    private sealed class ThrowingRepository : IMovieRepository
    {
        public Task<int> Upsert(IReadOnlyCollection<Movie> movies, CancellationToken ct = default) =>
            throw new InvalidOperationException("upsert exploded");

        public Task<IReadOnlyList<Movie>> Get(IReadOnlyCollection<MovieId> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Movie>>([]);

        public Task<int> Count(CancellationToken ct = default) => Task.FromResult(0);
    }
}
