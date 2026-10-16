namespace PlotTwist.IntegrationTests;

using Dapper;
using Domain.Embeddings;
using Domain.Search;
using Infrastructure.Persistence;
using static TestData;

// What only a real Postgres can prove: the pending rule, criteria translated to SQL (including text[] containment),
// models with different dimensions in one table, and that searches actually use the partial HNSW index.
[Collection(PostgresCollection.Name)]
public sealed class PostgresMovieEmbeddingRepositoryTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly PostgresMovieRepository movies = new(postgres.DataSource);
    private readonly PostgresMovieEmbeddingRepository embeddings = new(postgres.DataSource);
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await postgres.Reset();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Pending_means_never_embedded_in_this_model_or_text_changed_since()
    {
        var alien = Movie("Alien");
        var jaws = Movie("Jaws");
        await movies.Upsert([alien, jaws], Ct);
        await embeddings.Save([Embedding(alien, Small, 1, 0, 0)], Ct);

        Assert.Equal([jaws.Id], await embeddings.ListPending(Small, Ct));
        Assert.Equal(2, (await embeddings.ListPending(Large, Ct)).Count);

        await movies.Upsert([Movie("Alien", summary: "A rewritten plot.")], Ct);
        Assert.Contains(alien.Id, await embeddings.ListPending(Small, Ct));
    }

    [Fact]
    public async Task Results_are_ordered_by_cosine_similarity()
    {
        var far = Movie("Far");
        var close = Movie("Close");
        var exact = Movie("Exact");
        await movies.Upsert([far, close, exact], Ct);
        await embeddings.Save(
            [Embedding(far, Small, 0, 1, 0), Embedding(close, Small, 1, 0.1f, 0), Embedding(exact, Small, 1, 0, 0)],
            Ct
        );

        var matches = await embeddings.FindSimilar(new Embedding([1f, 0f, 0f]), Small, MovieCriteria.None, 10, Ct);

        Assert.Equal(["Exact", "Close", "Far"], matches.Select(m => m.Movie.Title));
        Assert.Equal(1.0, matches[0].Similarity, precision: 5);
    }

    [Fact]
    public async Task Criteria_filter_in_the_database_by_year_range_and_genre()
    {
        Domain.Movies.Movie[] all =
        [
            Movie("Nineties drama", 1995, "drama"),
            Movie("Nineties romcom", 1996, "rom-com"),
            Movie("Eighties drama", 1985, "crime drama"),
            Movie("Recent drama", 2015, "drama"),
        ];
        await movies.Upsert(all, Ct);
        await embeddings.Save(all.Select(m => Embedding(m, Small, 1, 0, 0)).ToList(), Ct);

        Assert.Equal(["Nineties drama"], await Titles(MovieCriteria.Create(1990, 1999, "drama")));
        Assert.Equal(["Nineties romcom"], await Titles(MovieCriteria.Create(1990, 1999, "comedy")));
        Assert.Equal(
            ["Eighties drama", "Nineties drama"],
            await Titles(MovieCriteria.Create(releasedTo: 1999, genre: "drama"))
        );
    }

    [Fact]
    public async Task Models_with_different_dimensions_live_side_by_side()
    {
        var alien = Movie("Alien");
        await movies.Upsert([alien], Ct);
        await embeddings.Save([Embedding(alien, Small, 1, 0, 0), Embedding(alien, Large, 0, 0, 0, 1)], Ct);
        var index = new PostgresVectorIndex(postgres.DataSource, postgres.Database);
        await index.Build(Small, rebuild: false, Ct);
        await index.Build(Large, rebuild: false, Ct);

        Assert.Equal((1, 1), (await embeddings.Count(Small, Ct), await embeddings.Count(Large, Ct)));
        Assert.Single(await embeddings.FindSimilar(new Embedding([0f, 0f, 0f, 1f]), Large, MovieCriteria.None, 5, Ct));
    }

    [Fact]
    public async Task Searches_use_the_models_partial_hnsw_index()
    {
        var films = Enumerable.Range(0, 200).Select(i => Movie($"Film {i}")).ToList();
        await movies.Upsert(films, Ct);
        await embeddings.Save(films.Select((m, i) => Embedding(m, Small, i, 1, i % 7)).ToList(), Ct);
        var index = await new PostgresVectorIndex(postgres.DataSource, postgres.Database).Build(
            Small,
            rebuild: false,
            Ct
        );

        await using var connection = await postgres.DataSource.OpenConnectionAsync(Ct);
        await connection.ExecuteAsync("SET enable_seqscan = off"); // tiny table: make the planner show its index choice
        var plan = string.Join(
            "\n",
            await connection.QueryAsync<string>(
                """
                EXPLAIN SELECT e.movie_id FROM movie_embeddings e JOIN movies m ON m.id = e.movie_id
                WHERE e.model = 'test-small'
                ORDER BY (e.embedding::vector(3)) <=> '[1,0,0]'::vector(3) LIMIT 5
                """
            )
        );

        Assert.True(index.Exists);
        Assert.Contains(index.Name, plan);
    }

    [Fact]
    public async Task Brute_force_listing_returns_each_movie_with_its_vector()
    {
        var alien = Movie("Alien");
        await movies.Upsert([alien], Ct);
        await embeddings.Save([Embedding(alien, Small, 1, 2, 3)], Ct);

        var (movie, vector) = Assert.Single(await embeddings.ListAll(Small, Ct).ToListAsync(Ct));

        Assert.Equal("Alien", movie.Title);
        Assert.Equal([1f, 2f, 3f], vector.Values.ToArray());
    }

    private async Task<IEnumerable<string>> Titles(MovieCriteria criteria) =>
        (await embeddings.FindSimilar(new Embedding([1f, 0f, 0f]), Small, criteria, 10, Ct))
            .Select(m => m.Movie.Title)
            .Order();
}
