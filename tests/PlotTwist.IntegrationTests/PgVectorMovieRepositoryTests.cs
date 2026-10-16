namespace PlotTwist.IntegrationTests;

using CommunityToolkit.VectorData.PgVector;
using Npgsql;
using PlotTwist.Domain.Embeddings;
using PlotTwist.Domain.Movies;
using PlotTwist.Domain.Search;
using PlotTwist.Infrastructure.Persistence;

// The parts that only a real Postgres can prove: the table the connector creates, that MovieCriteria
// translates to SQL (including the text[] genre filter), and that similarity ordering comes back intact.
public sealed class PgVectorMovieRepositoryTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const int Dimensions = 3;

    private NpgsqlDataSource dataSource = null!;
    private PgVectorMovieRepository repository = null!;

    public async ValueTask InitializeAsync()
    {
        var builder = new NpgsqlDataSourceBuilder(postgres.ConnectionString);
        builder.UseVector();
        dataSource = builder.Build();

        // A collection per test class instance, so tests don't see each other's rows.
        var collection = new PostgresVectorStore(dataSource, ownsDataSource: false)
            .GetCollection<int, MovieRecord>($"movies_{Guid.NewGuid():N}", MovieRecordDefinition.Create(Dimensions));
        repository = new PgVectorMovieRepository(collection, dataSource);
        await repository.EnsureCreated(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => dataSource.DisposeAsync();

    [Fact]
    public async Task Saved_movies_round_trip_through_the_store()
    {
        await Save(Movie(1, "Alien", 1979, "sci-fi, horror", [1, 0, 0]));

        var match = Assert.Single(await Find([1, 0, 0], MovieCriteria.None));

        Assert.Equal("Alien", match.Movie.Title);
        Assert.Equal(["science fiction", "horror"], match.Movie.Genres.Select(g => g.Value));
        Assert.Equal(new Uri("https://en.wikipedia.org/wiki/Alien"), match.Movie.WikipediaUrl);
        Assert.Equal(1.0, match.Similarity, precision: 5);
    }

    [Fact]
    public async Task Results_are_ordered_by_cosine_similarity()
    {
        await Save(
            Movie(1, "Far", 1990, "drama", [0, 1, 0]),
            Movie(2, "Close", 1990, "drama", [1, 0.1f, 0]),
            Movie(3, "Exact", 1990, "drama", [1, 0, 0]));

        var titles = (await Find([1, 0, 0], MovieCriteria.None)).Select(m => m.Movie.Title);

        Assert.Equal(["Exact", "Close", "Far"], titles);
    }

    [Fact]
    public async Task Criteria_filter_in_the_database_by_year_range_and_genre()
    {
        await Save(
            Movie(1, "Nineties drama", 1995, "drama", [1, 0, 0]),
            Movie(2, "Nineties romcom", 1996, "rom-com", [1, 0, 0]),
            Movie(3, "Eighties drama", 1985, "crime drama", [1, 0, 0]),
            Movie(4, "Recent drama", 2015, "drama", [1, 0, 0]));

        Assert.Equal(["Nineties drama"], Titles(await Find([1, 0, 0], MovieCriteria.Create(1990, 1999, "drama"))));
        Assert.Equal(["Nineties romcom"], Titles(await Find([1, 0, 0], MovieCriteria.Create(1990, 1999, "comedy"))));
        Assert.Equal(["Eighties drama", "Nineties drama"], Titles(await Find([1, 0, 0], MovieCriteria.Create(releasedTo: 1999, genre: "drama"))));
    }

    [Fact]
    public async Task Saving_the_same_id_again_replaces_the_movie()
    {
        await Save(Movie(1, "Old title", 1990, "drama", [1, 0, 0]));
        await Save(Movie(1, "New title", 1990, "drama", [1, 0, 0]));

        Assert.Equal(1, await repository.Count(TestContext.Current.CancellationToken));
        Assert.Equal("New title", Assert.Single(await Find([1, 0, 0], MovieCriteria.None)).Movie.Title);
    }

    [Fact]
    public async Task Brute_force_listing_includes_embeddings()
    {
        await Save(Movie(1, "Alien", 1979, "horror", [1, 2, 3]));

        var movie = Assert.Single(await repository.ListEmbedded(TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken));

        Assert.Equal([1f, 2f, 3f], movie.PlotEmbedding!.Values.ToArray());
    }

    private Task Save(params Movie[] movies) => repository.Save(movies, TestContext.Current.CancellationToken);

    private Task<IReadOnlyList<MovieMatch>> Find(float[] vector, MovieCriteria criteria) =>
        repository.FindSimilar(new Embedding(vector), criteria, top: 10, TestContext.Current.CancellationToken);

    private static IEnumerable<string> Titles(IEnumerable<MovieMatch> matches) => matches.Select(m => m.Movie.Title).Order();

    private static Movie Movie(int id, string title, int year, string genres, float[] embedding) =>
        new(
            MovieId.Create(id),
            title,
            ReleaseYear.Create(year),
            Genre.ParseList(genres),
            "American",
            "Someone",
            $"{title} happens.",
            new Uri($"https://en.wikipedia.org/wiki/{title.Replace(' ', '_')}"),
            new Embedding(embedding));
}
