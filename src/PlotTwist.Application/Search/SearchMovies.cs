namespace PlotTwist.Application.Search;

using PlotTwist.Domain.Search;

public sealed record SearchMovies(PlotDescription Plot, MovieCriteria Criteria, int Top = SearchMovies.DefaultTop)
{
    public const int DefaultTop = 5;
    public const int MaxTop = 50;
}

public sealed record SearchMoviesResult(
    PlotDescription Plot,
    MovieCriteria Criteria,
    IReadOnlyList<MovieMatch> Matches,
    SearchMetadata Metadata
);

public sealed record SearchMetadata(
    string EmbeddingModelName,
    int Dimensions,
    TimeSpan EmbeddingTime,
    TimeSpan SearchTime
)
{
    public TimeSpan TotalTime => EmbeddingTime + SearchTime;
}
