namespace PlotTwist.Core.Movies;

/// <summary>
/// The dataset's Genre column is free text ("sci-fi", "science fiction", "comedy, drama", "unknown").
/// Filters only work if the values are consistent, so collapse it into lowercase tags.
/// </summary>
public static class Genres
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sci-fi"] = "science fiction",
        ["scifi"] = "science fiction",
        ["sf"] = "science fiction",
        ["romcom"] = "romantic comedy",
        ["rom-com"] = "romantic comedy",
        ["animation"] = "animated",
        ["animated film"] = "animated",
        ["animated feature"] = "animated",
        ["noir"] = "film noir",
        ["biopic"] = "biography",
        ["biographical"] = "biography",
        ["war film"] = "war",
        ["romantic"] = "romance",
    };

    /// <summary>Compound genres also get their base tags, so a "comedy" filter finds "romantic comedy".</summary>
    private static readonly string[] BaseGenres =
    [
        "action", "adventure", "animated", "comedy", "crime", "drama", "family", "fantasy", "horror",
        "musical", "mystery", "romance", "science fiction", "thriller", "war", "western",
    ];

    public static List<string> Parse(string raw) =>
        raw.Split([',', '/', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(g => g is not ("" or "unknown" or "-"))
            .SelectMany(g => BaseGenres.Where(b => b != g && g.Contains(b)).Prepend(g))
            .Distinct()
            .ToList();

    public static string Normalize(string genre)
    {
        var g = genre.Trim().ToLowerInvariant();
        return Aliases.GetValueOrDefault(g, g);
    }
}
