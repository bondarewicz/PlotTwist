namespace PlotTwist.UnitTests.Application;

using Fakes;
using PlotTwist.Application.Embedding;
using PlotTwist.Domain.Movies;

public sealed class EmbedMoviesHandlerTests
{
    private readonly InMemoryMovieStore store = new();
    private readonly InMemoryIngestion runs = new();

    [Fact]
    public async Task Embeds_every_pending_film_in_batches()
    {
        await Seed(5);
        var embedder = new FakePlotEmbedder();

        var result = await Handler(embedder)
            .Handle(new EmbedMovies(BatchSize: 2), ct: TestContext.Current.CancellationToken);

        Assert.Equal(5, result.Embedded);
        Assert.Equal([1, 2, 2], embedder.BatchSizes.Order());
        Assert.Empty(await store.ListPending(embedder.Model, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_second_run_has_nothing_to_do()
    {
        await Seed(3);
        await Handler(new FakePlotEmbedder()).Handle(new EmbedMovies(), ct: TestContext.Current.CancellationToken);

        var second = await Handler(new FakePlotEmbedder())
            .Handle(new EmbedMovies(), ct: TestContext.Current.CancellationToken);

        Assert.Equal((0, 0), (second.Pending, second.Embedded));
    }

    [Fact]
    public async Task A_film_whose_text_changed_is_embedded_again()
    {
        await store.Upsert(
            [Movies.Create(title: "Alien", url: "https://en.wikipedia.org/wiki/Alien")],
            TestContext.Current.CancellationToken
        );
        await Handler(new FakePlotEmbedder()).Handle(new EmbedMovies(), ct: TestContext.Current.CancellationToken);

        await store.Upsert(
            [Movies.Create(title: "Alien", summary: "A new plot summary.", url: "https://en.wikipedia.org/wiki/Alien")],
            TestContext.Current.CancellationToken
        );
        var result = await Handler(new FakePlotEmbedder())
            .Handle(new EmbedMovies(), ct: TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Embedded);
    }

    [Fact]
    public async Task A_failure_keeps_what_was_saved_so_the_next_run_resumes()
    {
        await Seed(6);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Handler(new FakePlotEmbedder(failOnCall: 3))
                .Handle(new EmbedMovies(BatchSize: 2), ct: TestContext.Current.CancellationToken)
        );
        var resumed = await Handler(new FakePlotEmbedder())
            .Handle(new EmbedMovies(BatchSize: 2), ct: TestContext.Current.CancellationToken);

        Assert.Equal(2, resumed.Pending);
        Assert.Contains(
            runs.Runs.Values,
            r => r is { Kind: "embed", Status: "failed", Error: "model went away", Counts.Embedded: 4 }
        );
    }

    [Fact]
    public async Task A_cancelled_run_is_recorded_as_cancelled_with_its_partial_count()
    {
        await Seed(6);
        using var cancelled = new CancellationTokenSource();
        var embedder = new FakePlotEmbedder(cancelAfterCall: 2, cancelSource: cancelled);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Handler(embedder).Handle(new EmbedMovies(BatchSize: 2, MaxConcurrency: 1), ct: cancelled.Token)
        );

        Assert.Contains(
            runs.Runs.Values,
            r => r is { Kind: "embed", Status: "cancelled", Error: null, Counts.Embedded: 2 }
        );
    }

    [Fact]
    public async Task A_providers_own_timeout_is_recorded_as_failed_not_cancelled()
    {
        await Seed(3);

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            Handler(new FakePlotEmbedder(failOnCall: 1, failWith: new TaskCanceledException("timed out")))
                .Handle(new EmbedMovies(), ct: TestContext.Current.CancellationToken)
        );

        Assert.Contains(runs.Runs.Values, r => r is { Kind: "embed", Status: "failed", Error: "timed out" });
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public async Task Rejects_a_non_positive_batch_size_or_concurrency(int batchSize, int maxConcurrency)
    {
        await Seed(1);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            Handler(new FakePlotEmbedder())
                .Handle(new EmbedMovies(batchSize, maxConcurrency), ct: TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Concurrent_batches_embed_everything_exactly_once()
    {
        await Seed(50);

        var result = await Handler(new FakePlotEmbedder())
            .Handle(new EmbedMovies(BatchSize: 3, MaxConcurrency: 4), ct: TestContext.Current.CancellationToken);

        Assert.Equal(50, result.Embedded);
        Assert.Equal(50, await store.Count(new FakePlotEmbedder().Model, TestContext.Current.CancellationToken));
    }

    private Task Seed(int count) =>
        store.Upsert(
            Enumerable.Range(0, count).Select(_ => Movies.Create()).ToList(),
            TestContext.Current.CancellationToken
        );

    private EmbedMoviesHandler Handler(FakePlotEmbedder embedder) => new(embedder, store, store, runs);
}
