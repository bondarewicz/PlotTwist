namespace PlotTwist.UnitTests.Application;

using PlotTwist.Application.Ingest;
using PlotTwist.UnitTests.Fakes;

public sealed class IngestMoviesHandlerTests
{
    private readonly InMemoryMovieRepository repository = new();
    private readonly FakePlotEmbedder embedder = new();

    [Fact]
    public async Task Embeds_and_saves_every_movie_after_creating_storage()
    {
        var handler = Handler(Movies.Create(), Movies.Create(), Movies.Create());

        var result = await handler.Handle(new IngestMovies(), ct: TestContext.Current.CancellationToken);

        Assert.True(repository.Created);
        Assert.Equal(3, result.Embedded);
        Assert.Equal(3, await repository.Count(TestContext.Current.CancellationToken));
        Assert.Equal("fake-embedder", result.EmbeddingModel);
    }

    [Fact]
    public async Task Embeds_in_batches()
    {
        var handler = Handler(Enumerable.Range(0, 5).Select(_ => Movies.Create()).ToArray());

        await handler.Handle(new IngestMovies(BatchSize: 2), ct: TestContext.Current.CancellationToken);

        Assert.Equal([2, 2, 1], embedder.BatchSizes);
    }

    [Fact]
    public async Task Limit_and_origin_select_what_gets_ingested()
    {
        var handler = Handler(Movies.Create(origin: "British"), Movies.Create(origin: "American"), Movies.Create(origin: "american"), Movies.Create(origin: "American"));

        var result = await handler.Handle(new IngestMovies(Limit: 2, Origin: "American"), ct: TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Embedded);
    }

    [Fact]
    public async Task Reports_progress_per_batch()
    {
        var reports = new List<IngestProgress>();
        var handler = Handler(Movies.Create(), Movies.Create(), Movies.Create());

        await handler.Handle(new IngestMovies(BatchSize: 2), new SynchronousProgress<IngestProgress>(reports.Add), TestContext.Current.CancellationToken);

        Assert.Equal([2, 3], reports.Select(r => r.Embedded));
    }

    private IngestMoviesHandler Handler(params PlotTwist.Domain.Movies.Movie[] movies) =>
        new(new FakeMovieDataset(movies), embedder, repository, repository);

    private sealed class SynchronousProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
