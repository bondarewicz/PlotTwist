namespace PlotTwist.Domain.Movies;

using System.Security.Cryptography;
using System.Text;

public sealed class Movie
{
    public Movie(
        Uri wikipediaUrl,
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

        WikipediaUrl =
            wikipediaUrl ?? throw new DomainException($"'{title}' has no Wikipedia page, so it has no identity.");
        Id = MovieId.FromSourceKey(SourceKeyOf(wikipediaUrl));
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

    public Uri WikipediaUrl { get; }

    public string SourceKey => SourceKeyOf(WikipediaUrl);

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

    private static string SourceKeyOf(Uri url) => url.GetLeftPart(UriPartial.Path).TrimEnd('/').ToLowerInvariant();
}
