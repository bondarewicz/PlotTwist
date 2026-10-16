namespace PlotTwist.Domain.Movies;

// Position of the film in the source dataset: stable for the same file, so re-ingesting upserts.
public sealed record MovieId
{
    private MovieId(int value) => Value = value;

    public int Value { get; }

    public static MovieId Create(int value) =>
        value > 0 ? new MovieId(value) : throw new ArgumentOutOfRangeException(nameof(value), value, "Movie id must be positive.");

    public override string ToString() => Value.ToString();
}
