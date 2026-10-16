namespace PlotTwist.Domain.Movies;

using System.Security.Cryptography;
using System.Text;

// Aggregate root: one film as published by its source. Its embeddings are derived data with their own
// lifecycle (per model, recomputed when SearchableText changes), so they live in MovieEmbedding, not here.
public sealed class Movie
{
    public Movie(
        Uri wikipediaUrl,
        string title,
        ReleaseYear releaseYear,
        IReadOnlyList<Genre> genres,
        IReadOnlyList<string> origins,
        string director,
        string plotSummary)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("A movie needs a title.");
        if (string.IsNullOrWhiteSpace(plotSummary))
            throw new DomainException($"'{title}' has no plot summary, so it cannot be searched.");

        WikipediaUrl = wikipediaUrl ?? throw new DomainException($"'{title}' has no Wikipedia page, so it has no identity.");
        Id = MovieId.FromSourceKey(SourceKeyOf(wikipediaUrl));
        Title = title.Trim();
        ReleaseYear = releaseYear;
        Genres = genres;
        Origins = origins.Select(o => o.Trim()).Where(o => o.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Director = director.Trim();
        PlotSummary = plotSummary.Trim();
    }

    public MovieId Id { get; }

    public Uri WikipediaUrl { get; }

    public string SourceKey => SourceKeyOf(WikipediaUrl);

    public string Title { get; }

    public ReleaseYear ReleaseYear { get; }

    public IReadOnlyList<Genre> Genres { get; }

    // Co-productions are listed once per country in the source ("American", "British"): one film, several origins.
    public IReadOnlyList<string> Origins { get; }

    public string Director { get; }

    public string PlotSummary { get; }

    // What gets embedded. Title, year and genres help retrieval as much as the plot itself does,
    // so changing this text changes search quality - measure it with the retrieval evaluation.
    public string SearchableText => $"{Title} ({ReleaseYear}). {string.Join(", ", Genres)}. {PlotSummary}";

    // Changes exactly when SearchableText changes: an embedding made from another hash is stale.
    public string SearchableTextHash => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(SearchableText)));

    public override string ToString() => $"{Title} ({ReleaseYear})";

    // One page is one film. Scheme, host case and trailing slashes don't make a different page.
    private static string SourceKeyOf(Uri url) => url.GetLeftPart(UriPartial.Path).TrimEnd('/').ToLowerInvariant();
}
