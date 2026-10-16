namespace PlotTwist.UnitTests.Domain;

using PlotTwist.Domain;
using PlotTwist.Domain.Movies;

public sealed class WikipediaPageTests
{
    [Fact]
    public void Id_and_key_match_what_is_already_stored_for_the_matrix()
    {
        var page = WikipediaPage.Create(new Uri("https://en.wikipedia.org/wiki/The_Matrix"));

        Assert.Equal("https://en.wikipedia.org/wiki/the_matrix", page.Key);
        Assert.Equal(Guid.Parse("5b90ecdf-53b7-1917-e69c-dbb4fd6b65bb"), MovieId.For(page).Value);
    }

    [Theory]
    [InlineData("https://en.wikipedia.org/wiki/Alien_(film)")]
    [InlineData("HTTPS://EN.wikipedia.org/wiki/alien_(film)")]
    [InlineData("https://en.wikipedia.org/wiki/Alien_(film)/")]
    public void Scheme_case_and_trailing_slash_dont_affect_identity(string url)
    {
        var canonical = WikipediaPage.Create(new Uri("https://en.wikipedia.org/wiki/Alien_(film)"));
        var variant = WikipediaPage.Create(new Uri(url));

        Assert.Equal(canonical.Key, variant.Key);
        Assert.Equal(canonical, variant);
        Assert.Equal(MovieId.For(canonical), MovieId.For(variant));
    }

    [Fact]
    public void Create_rejects_a_non_wikipedia_host() =>
        Assert.Throws<DomainException>(() => WikipediaPage.Create(new Uri("https://www.imdb.com/title/tt0133093/")));

    [Fact]
    public void Create_rejects_a_relative_url() =>
        Assert.Throws<DomainException>(() => WikipediaPage.Create(new Uri("wiki/The_Matrix", UriKind.Relative)));

    [Fact]
    public void Create_rejects_a_null_url() => Assert.Throws<DomainException>(() => WikipediaPage.Create(null));
}
