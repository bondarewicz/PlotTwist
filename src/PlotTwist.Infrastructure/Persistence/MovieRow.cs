namespace PlotTwist.Infrastructure.Persistence;

using Domain.Movies;

internal class MovieRow
{
    public const string Columns =
        "m.id, m.title, m.release_year, m.genres, m.origins, m.director, m.plot_summary, m.wikipedia_url";

    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public int ReleaseYear { get; init; }
    public string[] Genres { get; init; } = [];
    public string[] Origins { get; init; } = [];
    public string Director { get; init; } = "";
    public string PlotSummary { get; init; } = "";
    public string WikipediaUrl { get; init; } = "";

    public Movie ToDomain() =>
        new(
            WikipediaPage.Create(new Uri(WikipediaUrl)),
            Title,
            PlotTwist.Domain.Movies.ReleaseYear.Create(ReleaseYear),
            Genres.Select(Genre.Create).ToList(),
            Origins,
            Director,
            PlotSummary
        );
}
