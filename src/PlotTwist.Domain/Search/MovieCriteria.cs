namespace PlotTwist.Domain.Search;

using Movies;

public sealed record MovieCriteria
{
    public static readonly MovieCriteria None = new(null, null, null);

    private MovieCriteria(ReleaseYear? releasedFrom, ReleaseYear? releasedTo, Genre? genre)
    {
        ReleasedFrom = releasedFrom;
        ReleasedTo = releasedTo;
        Genre = genre;
    }

    public ReleaseYear? ReleasedFrom { get; }

    public ReleaseYear? ReleasedTo { get; }

    public Genre? Genre { get; }

    public bool IsEmpty => ReleasedFrom is null && ReleasedTo is null && Genre is null;

    public static MovieCriteria Create(int? releasedFrom = null, int? releasedTo = null, string? genre = null)
    {
        var from = releasedFrom is { } f ? ReleaseYear.Create(f) : null;
        var to = releasedTo is { } t ? ReleaseYear.Create(t) : null;
        if (from is not null && to is not null && from.Value > to.Value)
            throw new DomainException($"Release year range {from}-{to} is the wrong way round.");

        return new MovieCriteria(from, to, string.IsNullOrWhiteSpace(genre) ? null : Genre.Create(genre));
    }

    public bool IsSatisfiedBy(Movie movie) =>
        (ReleasedFrom is null || movie.ReleaseYear.Value >= ReleasedFrom.Value)
        && (ReleasedTo is null || movie.ReleaseYear.Value <= ReleasedTo.Value)
        && (Genre is null || movie.Genres.Contains(Genre));

    public override string ToString() =>
        IsEmpty
            ? "none"
            : string.Join(
                ", ",
                new[]
                {
                    ReleasedFrom is null ? null : $"year >= {ReleasedFrom}",
                    ReleasedTo is null ? null : $"year <= {ReleasedTo}",
                    Genre is null ? null : $"genre = {Genre}",
                }.OfType<string>()
            );
}
