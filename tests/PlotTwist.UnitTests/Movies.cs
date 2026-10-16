namespace PlotTwist.UnitTests;

using PlotTwist.Application.Abstractions;
using PlotTwist.Domain.Movies;

internal static class Movies
{
    private static int next;

    public static Movie Create(
        string title = "Some Film",
        int year = 1995,
        string genres = "drama",
        string summary = "Something happens.",
        string origin = "American",
        string? url = null
    ) =>
        new(
            WikipediaPage.Create(
                new Uri(url ?? $"https://en.wikipedia.org/wiki/Film_{Interlocked.Increment(ref next)}")
            ),
            title,
            ReleaseYear.Create(year),
            Genre.ParseList(genres),
            [origin],
            "Some Director",
            summary
        );

    public static RawMovieRow Row(
        int rowNumber,
        string title,
        string year,
        string url,
        string origin = "American",
        string genre = "drama",
        string summary = "Something happens."
    ) => new(rowNumber, year, title, origin, "Some Director", "", genre, url, "", summary);
}
