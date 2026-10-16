namespace PlotTwist.Domain.Movies;

public sealed record ReleaseYear
{
    private const int Earliest = 1888;
    private const int Latest = 2100;

    private ReleaseYear(int value) => Value = value;

    public int Value { get; }

    public static ReleaseYear Create(int value) =>
        value is >= Earliest and <= Latest
            ? new ReleaseYear(value)
            : throw new DomainException($"Release year {value} is outside {Earliest}-{Latest}.");

    public override string ToString() => Value.ToString();
}
