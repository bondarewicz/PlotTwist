namespace PlotTwist.Application.Search;

using PlotTwist.Domain.Search;

public sealed record FindMovies(PlotDescription Description, MovieCriteria Criteria, int Top = 5)
{
    public const int DefaultTop = 5; // keep in sync with the Top default above
    public const int MaxTop = 50;
}

public sealed record FindMoviesResult(
    PlotDescription Description,
    MovieCriteria Criteria,
    IReadOnlyList<MovieMatch> Matches,
    SearchMetadata Metadata
);

// How the answer was produced, so a caller can judge it: which model, how long each step took,
// and whether the filters were what limited the results.
public sealed record SearchMetadata(string EmbeddingModel, int Dimensions, TimeSpan EmbeddingTime, TimeSpan SearchTime)
{
    public TimeSpan TotalTime => EmbeddingTime + SearchTime;
}
