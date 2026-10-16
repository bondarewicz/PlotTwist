namespace PlotTwist.UnitTests.Application;

using PlotTwist.Application.Search;
using PlotTwist.Domain;
using PlotTwist.Domain.Embeddings;
using PlotTwist.Domain.Search;
using PlotTwist.UnitTests.Fakes;

public sealed class FindMoviesHandlerTests
{
    private readonly InMemoryMovieRepository repository = new();
    private readonly FindMoviesHandler handler;

    public FindMoviesHandlerTests()
    {
        handler = new FindMoviesHandler(new FakePlotEmbedder(), repository);
    }

    [Fact]
    public async Task Passes_the_criteria_to_the_store_and_returns_its_matches_with_metadata()
    {
        await Seed(Movies.Create(year: 1995, genres: "drama"), Movies.Create(year: 2015, genres: "drama"));
        var criteria = MovieCriteria.Create(1990, 1999);

        var result = await handler.Handle(new FindMovies(PlotDescription.Create("astronauts"), criteria), TestContext.Current.CancellationToken);

        Assert.Same(criteria, repository.LastCriteria);
        Assert.Equal(1995, Assert.Single(result.Matches).Movie.ReleaseYear.Value);
        Assert.Equal("fake-embedder", result.Metadata.EmbeddingModel);
        Assert.Equal(2, result.Metadata.Dimensions);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(FindMovies.MaxTop + 1)]
    public async Task Rejects_result_counts_outside_the_allowed_range(int top) =>
        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new FindMovies(PlotDescription.Create("x"), MovieCriteria.None, top), TestContext.Current.CancellationToken));

    private async Task Seed(params PlotTwist.Domain.Movies.Movie[] movies)
    {
        foreach (var movie in movies)
            movie.AssignPlotEmbedding(new Embedding([1f, 1f]));
        await repository.Save(movies, TestContext.Current.CancellationToken);
    }
}
