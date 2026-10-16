namespace PlotTwist.Domain.Movies;

using System.Security.Cryptography;
using System.Text;

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
        value != Guid.Empty ? new MovieId(value) : throw new DomainException("Movie id cannot be empty.");

    public override string ToString() => Value.ToString();
}
