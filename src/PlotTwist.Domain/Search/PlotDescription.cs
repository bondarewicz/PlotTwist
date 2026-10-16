namespace PlotTwist.Domain.Search;

public sealed record PlotDescription
{
    public const int MaxLength = 500;

    private PlotDescription(string value) => Value = value;

    public string Value { get; }

    public static PlotDescription Create(string? raw)
    {
        var normalized = string.IsNullOrWhiteSpace(raw)
            ? string.Empty
            : string.Join(' ', raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return normalized.Length switch
        {
            0 => throw new DomainException("Describe the film: the plot description is empty."),
            > MaxLength => throw new DomainException($"The plot description is longer than {MaxLength} characters."),
            _ => new PlotDescription(normalized),
        };
    }

    public override string ToString() => Value;
}
