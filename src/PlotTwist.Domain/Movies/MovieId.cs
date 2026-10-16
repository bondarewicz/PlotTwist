namespace PlotTwist.Domain.Movies;

using System.Security.Cryptography;
using System.Text;

// Stable identity derived from the film's source key (its Wikipedia page): the same film gets the same id on
// every import, whatever row it sits on, so re-importing updates instead of duplicating and embeddings stay attached.
public sealed record MovieId
{
    private MovieId(Guid value) => Value = value;

    public Guid Value { get; }

    public static MovieId FromSourceKey(string sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
            throw new DomainException("A movie needs a source key to derive its id.");

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sourceKey.Trim().ToLowerInvariant()));
        return new MovieId(new Guid(hash.AsSpan(0, 16)));
    }

    public static MovieId FromValue(Guid value) =>
        value != Guid.Empty ? new MovieId(value) : throw new ArgumentException("Movie id cannot be empty.", nameof(value));

    public override string ToString() => Value.ToString();
}
