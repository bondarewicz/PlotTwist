namespace PlotTwist.Domain.Search;

public static class MatchConfidence
{
    public const double AmbiguousMargin = 0.02;

    public static bool IsAmbiguous(IReadOnlyList<MovieMatch> matches) =>
        matches.Count >= 2 && Margin(matches) < AmbiguousMargin;

    public static double? Margin(IReadOnlyList<MovieMatch> matches) =>
        matches.Count >= 2 ? matches[0].Similarity - matches[1].Similarity : null;
}
