namespace PlotTwist.IntegrationTests;

using Infrastructure.Persistence;
using static TestData;

[Collection(PostgresCollection.Name)]
public sealed class PostgresMovieRepositoryTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly PostgresMovieRepository movies = new(postgres.DataSource);

    public async ValueTask InitializeAsync() => await postgres.Reset();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Upsert_counts_new_movies_and_round_trips_every_field()
    {
        var alien = Movie("Alien", 1979, "sci-fi, horror", "A crew meets a creature.", "American", "British");

        Assert.Equal(1, await movies.Upsert([alien], TestContext.Current.CancellationToken));

        var stored = Assert.Single(await movies.Get([alien.Id], TestContext.Current.CancellationToken));
        Assert.Equal((alien.Id, "Alien", 1979), (stored.Id, stored.Title, stored.ReleaseYear.Value));
        Assert.Equal(["science fiction", "horror"], stored.Genres.Select(g => g.Value));
        Assert.Equal(["American", "British"], stored.Origins);
        Assert.Equal(alien.SearchableTextHash, stored.SearchableTextHash);
    }

    [Fact]
    public async Task Re_importing_updates_in_place_and_counts_nothing_new()
    {
        await movies.Upsert([Movie("Alien", summary: "Old summary.")], TestContext.Current.CancellationToken);

        var created = await movies.Upsert(
            [Movie("Alien", summary: "New summary."), Movie("Jaws")],
            TestContext.Current.CancellationToken
        );

        Assert.Equal(1, created);
        Assert.Equal(2, await movies.Count(TestContext.Current.CancellationToken));
        Assert.Equal(
            "New summary.",
            (await movies.Get([Movie("Alien").Id], TestContext.Current.CancellationToken))[0].PlotSummary
        );
    }
}
