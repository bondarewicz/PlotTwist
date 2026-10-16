namespace PlotTwist.UnitTests.Fakes;

using System.Runtime.CompilerServices;
using PlotTwist.Application.Abstractions;
using PlotTwist.Domain.Movies;

internal sealed class FakeMovieDataset(params Movie[] movies) : IMovieDataset
{
    public string Name => "fake";

    public async IAsyncEnumerable<Movie> Read([EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var movie in movies)
            yield return movie;
        await Task.CompletedTask;
    }
}
