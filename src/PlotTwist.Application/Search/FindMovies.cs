namespace PlotTwist.Application.Search;

using PlotTwist.Domain.Search;

public sealed record FindMovies(PlotDescription Description, MovieCriteria Criteria, int Top = FindMovies.DefaultTop)
{
    public const int DefaultTop = 5;
    public const int MaxTop = 50;
}

public sealed record FindMoviesResult(
    PlotDescription Description,
    MovieCriteria Criteria,
    IReadOnlyList<MovieMatch> Matches,
    SearchMetadata Metadata
);

public sealed record SearchMetadata(string EmbeddingModel, int Dimensions, TimeSpan EmbeddingTime, TimeSpan SearchTime)
{
    public TimeSpan TotalTime => EmbeddingTime + SearchTime;
}
