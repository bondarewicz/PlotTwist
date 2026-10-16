namespace PlotTwist.UnitTests.Application;

using PlotTwist.Application.Embedding;
using PlotTwist.Domain.Movies;
using PlotTwist.UnitTests.Fakes;

public sealed class EmbedMoviesHandlerTests
{
    private readonly InMemoryMovieStore store = new();
    private readonly InMemoryIngestion runs = new();

    [Fact]
    public async Task Embeds_every_pending_film_in_batches()
    {
        await Seed(5);
        var embedder = new FakePlotEmbedder();

        var result = await Handler(embedder).Handle(new EmbedMovies(BatchSize: 2), ct: TestContext.Current.CancellationToken);

        Assert.Equal(5, result.Embedded);
        Assert.Equal([1, 2, 2], embedder.BatchSizes.Order());
        Assert.Empty(await store.ListPending(embedder.Model, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_second_run_has_nothing_to_do()
    {
        await Seed(3);
        await Handler(new FakePlotEmbedder()).Handle(new EmbedMovies(), ct: TestContext.Current.CancellationToken);

        var second = await Handler(new FakePlotEmbedder()).Handle(new EmbedMovies(), ct: TestContext.Current.CancellationToken);

        Assert.Equal((0, 0), (second.Pending, second.Embedded));
    }

    [Fact]
    public async Task A_film_whose_text_changed_is_embedded_again()
    {
        await store.Upsert([Movies.Create(title: "Alien", url: "https://en.wikipedia.org/wiki/Alien")], TestContext.Current.CancellationToken);
        await Handler(new FakePlotEmbedder()).Handle(new EmbedMovies(), ct: TestContext.Current.CancellationToken);

        await store.Upsert([Movies.Create(title: "Alien", summary: "A new plot summary.", url: "https://en.wikipedia.org/wiki/Alien")], TestContext.Current.CancellationToken);
        var result = await Handler(new FakePlotEmbedder()).Handle(new EmbedMovies(), ct: TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Embedded);
    }

    [Fact]
    public async Task A_failure_keeps_what_was_saved_so_the_next_run_resumes()
    {
        await Seed(6);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Handler(new FakePlotEmbedder(failOnCall: 3)).Handle(new EmbedMovies(BatchSize: 2), ct: TestContext.Current.CancellationToken));
        var resumed = await Handler(new FakePlotEmbedder()).Handle(new EmbedMovies(BatchSize: 2), ct: TestContext.Current.CancellationToken);

        Assert.Equal(2, resumed.Pending);
        Assert.Contains(runs.Runs.Values, r => r is { Kind: "embed", Status: "failed" });
    }

    [Fact]
    public async Task A_cancelled_run_is_recorded_rather_than_left_running()
    {
        await Seed(3);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Handler(new FakePlotEmbedder()).Handle(new EmbedMovies(), ct: cancelled.Token));

        Assert.Contains(runs.Runs.Values, r => r is { Kind: "embed", Status: "failed", Error: var e } && e!.StartsWith("Cancelled"));
    }

    [Fact]
    public async Task Concurrent_batches_embed_everything_exactly_once()
    {
        await Seed(50);

        var result = await Handler(new FakePlotEmbedder()).Handle(new EmbedMovies(BatchSize: 3, MaxConcurrency: 4), ct: TestContext.Current.CancellationToken);

        Assert.Equal(50, result.Embedded);
        Assert.Equal(50, await store.Count(new FakePlotEmbedder().Model, TestContext.Current.CancellationToken));
    }

    private Task Seed(int count) =>
        store.Upsert(Enumerable.Range(0, count).Select(_ => Movies.Create()).ToList(), TestContext.Current.CancellationToken);

    private EmbedMoviesHandler Handler(FakePlotEmbedder embedder) => new(embedder, store, store, runs);
}
