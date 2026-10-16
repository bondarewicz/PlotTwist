namespace PlotTwist.Domain.Movies;

using PlotTwist.Domain.Embeddings;

// Aggregate root: one film and the vector that represents its plot for search.
public sealed class Movie
{
    public Movie(
        MovieId id,
        string title,
        ReleaseYear releaseYear,
        IReadOnlyList<Genre> genres,
        string origin,
        string director,
        string plotSummary,
        Uri? wikipediaUrl,
        Embedding? plotEmbedding = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("A movie needs a title.");
        if (string.IsNullOrWhiteSpace(plotSummary))
            throw new DomainException($"'{title}' has no plot summary, so it cannot be searched.");

        Id = id;
        Title = title.Trim();
        ReleaseYear = releaseYear;
        Genres = genres;
        Origin = origin.Trim();
        Director = director.Trim();
        PlotSummary = plotSummary.Trim();
        WikipediaUrl = wikipediaUrl;
        PlotEmbedding = plotEmbedding;
    }

    public MovieId Id { get; }

    public string Title { get; }

    public ReleaseYear ReleaseYear { get; }

    public IReadOnlyList<Genre> Genres { get; }

    public string Origin { get; }

    public string Director { get; }

    public string PlotSummary { get; }

    public Uri? WikipediaUrl { get; }

    public Embedding? PlotEmbedding { get; private set; }

    public bool IsEmbedded => PlotEmbedding is not null;

    // What gets embedded. Title, year and genres help retrieval as much as the plot itself does,
    // so changing this text changes search quality - measure it with the eval before and after.
    public string SearchableText => $"{Title} ({ReleaseYear}). {string.Join(", ", Genres)}. {PlotSummary}";

    public void AssignPlotEmbedding(Embedding embedding) =>
        PlotEmbedding = embedding ?? throw new ArgumentNullException(nameof(embedding));

    public override string ToString() => $"{Title} ({ReleaseYear})";
}
