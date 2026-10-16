namespace PlotTwist.Domain.Movies;

// A genre tag as it is stored and filtered on: lowercase, with common aliases collapsed ("sci-fi" ->
// "science fiction"). The source data is free text, and filters only work if the vocabulary is consistent.
public sealed record Genre
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

    // Compound genres also carry their base tags, so filtering on "comedy" finds "romantic comedy".
    private static readonly string[] BaseGenres =
    [
        "action", "adventure", "animated", "comedy", "crime", "drama", "family", "fantasy", "horror",
        "musical", "mystery", "romance", "science fiction", "thriller", "war", "western",
    ];

    private static readonly string[] Unknown = ["", "unknown", "-"];

    private Genre(string value) => Value = value;

    public string Value { get; }

    public static Genre Create(string? value)
    {
        var normalized = Normalize(value);
        return Unknown.Contains(normalized)
            ? throw new DomainException($"'{value}' is not a genre.")
            : new Genre(normalized);
    }

    // Free-text genre column ("Crime drama, Sci-Fi") -> distinct tags, unknowns dropped.
    public static IReadOnlyList<Genre> ParseList(string? raw) =>
        (raw ?? "")
            .Split([',', '/', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(g => !Unknown.Contains(g))
            .SelectMany(g => BaseGenres.Where(b => b != g && g.Contains(b)).Prepend(g))
            .Distinct()
            .Select(g => new Genre(g))
            .ToList();

    public override string ToString() => Value;

    private static string Normalize(string? value)
    {
        var trimmed = (value ?? "").Trim().ToLowerInvariant();
        return Aliases.GetValueOrDefault(trimmed, trimmed);
    }
}
