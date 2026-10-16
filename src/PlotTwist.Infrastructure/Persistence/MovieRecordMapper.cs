namespace PlotTwist.Infrastructure.Persistence;

using PlotTwist.Domain.Embeddings;
using PlotTwist.Domain.Movies;

internal static class MovieRecordMapper
{
    public static MovieRecord ToRecord(Movie movie) => new()
    {
        Id = movie.Id.Value,
        Title = movie.Title,
        Year = movie.ReleaseYear.Value,
        Genres = movie.Genres.Select(g => g.Value).ToList(),
        Origin = movie.Origin,
        Director = movie.Director,
        Summary = movie.PlotSummary,
        WikiUrl = movie.WikipediaUrl?.ToString() ?? "",
        Embedding = movie.PlotEmbedding?.Values
            ?? throw new InvalidOperationException($"{movie} has no embedding; embed it before saving."),
    };

    // Stored values already passed the domain rules on the way in, so they are rebuilt through the same factories.
    public static Movie ToDomain(MovieRecord record) => new(
        MovieId.Create(record.Id),
        record.Title,
        ReleaseYear.Create(record.Year),
        record.Genres.Select(Genre.Create).ToList(),
        record.Origin,
        record.Director,
        record.Summary,
        Uri.TryCreate(record.WikiUrl, UriKind.Absolute, out var url) ? url : null,
        record.Embedding.IsEmpty ? null : new Embedding(record.Embedding.Span));
}
