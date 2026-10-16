namespace PlotTwist.UnitTests.Application;

using Fakes;
using PlotTwist.Application.Search;
using PlotTwist.Domain;
using PlotTwist.Domain.Embeddings;
using PlotTwist.Domain.Search;

public sealed class FindMoviesHandlerTests
{
    private readonly InMemoryMovieStore store = new();
    private readonly FakePlotEmbedder embedder = new();

    [Fact]
    public async Task Passes_the_criteria_to_the_store_and_returns_its_matches_with_metadata()
    {
        var nineties = Movies.Create(year: 1995);
        var recent = Movies.Create(year: 2015);
        await store.Upsert([nineties, recent], TestContext.Current.CancellationToken);
        await store.Save([Embedded(nineties), Embedded(recent)], TestContext.Current.CancellationToken);
        var criteria = MovieCriteria.Create(1990, 1999);

        var result = await new FindMoviesHandler(embedder, store).Handle(
            new FindMovies(PlotDescription.Create("astronauts"), criteria),
            TestContext.Current.CancellationToken
        );

        Assert.Same(criteria, store.LastCriteria);
        Assert.Equal(1995, Assert.Single(result.Matches).Movie.ReleaseYear.Value);
        Assert.Equal(("fake-embedder", 2), (result.Metadata.EmbeddingModel, result.Metadata.Dimensions));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(FindMovies.MaxTop + 1)]
    public async Task Rejects_result_counts_outside_the_allowed_range(int top) =>
        await Assert.ThrowsAsync<DomainException>(() =>
            new FindMoviesHandler(embedder, store).Handle(
                new FindMovies(PlotDescription.Create("x"), MovieCriteria.None, top),
                TestContext.Current.CancellationToken
            )
        );

    private MovieEmbedding Embedded(PlotTwist.Domain.Movies.Movie movie) =>
        new(movie.Id, embedder.Model, movie.SearchableTextHash, new Embedding([1f, 1f]));
}
