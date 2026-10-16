namespace PlotTwist.Application.Search;

using PlotTwist.Domain.Search;

public sealed record CompareSearchStrategies(
    PlotDescription Description,
    MovieCriteria Criteria,
    int Top = FindMovies.DefaultTop
);

public sealed record StrategyResult(string Strategy, IReadOnlyList<MovieMatch> Matches, TimeSpan Elapsed, string Notes);

public sealed record SearchComparison(
    PlotDescription Description,
    MovieCriteria Criteria,
    IReadOnlyList<StrategyResult> Strategies
);
