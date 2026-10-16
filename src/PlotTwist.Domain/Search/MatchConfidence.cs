namespace PlotTwist.Domain.Search;

public static class MatchConfidence
{
    // When the best film is less than this ahead of the runner-up the match is ambiguous:
    // the description fits both about equally, so ask for more detail or add criteria.
    public const double AmbiguousMargin = 0.02;

    public static bool IsAmbiguous(IReadOnlyList<MovieMatch> matches) =>
        matches.Count >= 2 && Margin(matches) < AmbiguousMargin;

    // Similarity lead of the best match over the runner-up; null with fewer than two matches.
    public static double? Margin(IReadOnlyList<MovieMatch> matches) =>
        matches.Count >= 2 ? matches[0].Similarity - matches[1].Similarity : null;
}
