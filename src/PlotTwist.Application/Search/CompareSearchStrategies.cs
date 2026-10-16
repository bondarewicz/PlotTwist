namespace PlotTwist.Application.Search;

using PlotTwist.Domain.Search;

public sealed record CompareSearchStrategies(
    PlotDescription Plot,
    MovieCriteria Criteria,
    int Top = SearchMovies.DefaultTop
);

public sealed record StrategyResult(string Strategy, IReadOnlyList<MovieMatch> Matches, TimeSpan Elapsed, string Notes);

public sealed record SearchComparison(
    PlotDescription Plot,
    MovieCriteria Criteria,
    IReadOnlyList<StrategyResult> Strategies
);
