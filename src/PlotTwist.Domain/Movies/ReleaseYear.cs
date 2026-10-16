namespace PlotTwist.Domain.Movies;

// 1888 is the oldest surviving film (Roundhay Garden Scene); anything earlier is a typo, not a movie.
public sealed record ReleaseYear : IComparable<ReleaseYear>
{
    private const int Earliest = 1888;
    private const int Latest = 2100;

    private ReleaseYear(int value) => Value = value;

    public int Value { get; }

    public static ReleaseYear Create(int value) =>
        value is >= Earliest and <= Latest
            ? new ReleaseYear(value)
            : throw new DomainException($"Release year {value} is outside {Earliest}-{Latest}.");

    public int CompareTo(ReleaseYear? other) => Value.CompareTo(other?.Value ?? int.MinValue);

    public override string ToString() => Value.ToString();
}
