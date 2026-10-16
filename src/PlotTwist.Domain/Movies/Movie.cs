namespace PlotTwist.Domain.Movies;

using System.Security.Cryptography;
using System.Text;

public sealed class Movie
{
    public Movie(
        WikipediaPage page,
        string title,
        ReleaseYear releaseYear,
        IReadOnlyList<Genre> genres,
        IReadOnlyList<string> origins,
        string director,
        string plotSummary
    )
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("A movie needs a title.");
        if (string.IsNullOrWhiteSpace(plotSummary))
            throw new DomainException($"'{title}' has no plot summary, so it cannot be searched.");

        WikipediaPage = page;
        Id = MovieId.For(page);
        Title = title.Trim();
        ReleaseYear = releaseYear;
        Genres = genres;
        Origins = origins
            .Select(o => o.Trim())
            .Where(o => o.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        Director = director.Trim();
        PlotSummary = plotSummary.Trim();
    }

    public MovieId Id { get; }

    public WikipediaPage WikipediaPage { get; }

    public string Title { get; }

    public ReleaseYear ReleaseYear { get; }

    public IReadOnlyList<Genre> Genres { get; }

    public IReadOnlyList<string> Origins { get; }

    public string Director { get; }

    public string PlotSummary { get; }

    public string SearchableText => $"{Title} ({ReleaseYear}). {string.Join(", ", Genres)}. {PlotSummary}";

    public string SearchableTextHash =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(SearchableText)));

    public override string ToString() => $"{Title} ({ReleaseYear})";
}
