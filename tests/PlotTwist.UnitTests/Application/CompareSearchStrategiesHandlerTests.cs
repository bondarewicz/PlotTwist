namespace PlotTwist.UnitTests.Application;

using Fakes;
using PlotTwist.Application.Search;
using PlotTwist.Domain;
using PlotTwist.Domain.Search;

public sealed class CompareSearchStrategiesHandlerTests
{
    private readonly InMemoryMovieStore store = new();
    private readonly FakePlotEmbedder embedder = new();

    [Fact]
    public async Task Rejects_a_top_of_zero() =>
        await Assert.ThrowsAsync<DomainException>(() =>
            new CompareSearchStrategiesHandler(embedder, store, store).Handle(
                new CompareSearchStrategies(PlotDescription.Create("x"), MovieCriteria.None, Top: 0),
                TestContext.Current.CancellationToken
            )
        );
}
