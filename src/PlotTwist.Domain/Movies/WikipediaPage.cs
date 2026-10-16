namespace PlotTwist.Domain.Movies;

public sealed record WikipediaPage
{
    private WikipediaPage(Uri url, string key)
    {
        Url = url;
        Key = key;
    }

    public Uri Url { get; }

    public string Key { get; }

    public static WikipediaPage Create(Uri? url)
    {
        if (url is null)
            throw new DomainException("A movie needs a Wikipedia page, so it has no identity.");
        if (!url.IsAbsoluteUri || url.Scheme is not ("http" or "https") || !IsWikipediaHost(url.Host))
            throw new DomainException($"'{url}' is not a Wikipedia page.");

        return new WikipediaPage(url, KeyOf(url));
    }

    public static string KeyOf(Uri url) => url.GetLeftPart(UriPartial.Path).TrimEnd('/').ToLowerInvariant();

    public bool Equals(WikipediaPage? other) => other is not null && Key == other.Key;

    public override int GetHashCode() => Key.GetHashCode();

    public override string ToString() => Url.ToString();

    private static bool IsWikipediaHost(string host) =>
        host.Equals("wikipedia.org", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".wikipedia.org", StringComparison.OrdinalIgnoreCase);
}
