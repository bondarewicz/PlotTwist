namespace PlotTwist.UnitTests.Application;

using PlotTwist.Application.Import;
using PlotTwist.UnitTests.Fakes;
using static PlotTwist.UnitTests.Movies;

public sealed class ImportMoviesHandlerTests
{
    private readonly InMemoryMovieStore store = new();

    [Fact]
    public async Task Imports_films_records_rejects_and_completes_the_run()
    {
        var ingestion = new InMemoryIngestion(
            Row(1, "Alien", "1979", "https://en.wikipedia.org/wiki/Alien"),
            Row(2, "Jaws", "1975", "https://en.wikipedia.org/wiki/Jaws"),
            Row(3, "Broken", "nope", "https://en.wikipedia.org/wiki/Broken"));

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
    public async Task Limit_keeps_the_first_films_only()
    {
        var ingestion = new InMemoryIngestion(
            Row(1, "A", "1990", "https://en.wikipedia.org/wiki/A"),
            Row(2, "B", "1991", "https://en.wikipedia.org/wiki/B"),
            Row(3, "C", "1992", "https://en.wikipedia.org/wiki/C"));

        var result = await Handler(ingestion).Handle(new ImportMovies(Limit: 2), TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Imported);
    }

    private ImportMoviesHandler Handler(InMemoryIngestion ingestion) => new(ingestion, ingestion, store, ingestion);
}
