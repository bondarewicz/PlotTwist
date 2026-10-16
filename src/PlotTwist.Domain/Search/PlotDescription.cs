namespace PlotTwist.Domain.Search;

// What someone remembers about a film, as it is embedded: trimmed, whitespace runs collapsed.
// Case is kept - embedding models handle it, and names ("Mars", "FBI") carry meaning.
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

        if (normalized.Length == 0)
            throw new DomainException("Describe the film: the plot description is empty.");
        if (normalized.Length > MaxLength)
            throw new DomainException($"The plot description is longer than {MaxLength} characters.");

        return new PlotDescription(normalized);
    }

    public override string ToString() => Value;
}
