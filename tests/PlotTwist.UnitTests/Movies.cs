namespace PlotTwist.UnitTests;

using PlotTwist.Domain.Movies;

internal static class Movies
{
    private static int nextId;

    public static Movie Create(
        string title = "Some Film",
        int year = 1995,
        string genres = "drama",
        string summary = "Something happens.",
        string origin = "American") =>
        new(
            MovieId.Create(Interlocked.Increment(ref nextId)),
            title,
            ReleaseYear.Create(year),
            Genre.ParseList(genres),
            origin,
            "Some Director",
            summary,
            null);
}
