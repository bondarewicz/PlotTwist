namespace PlotTwist.UnitTests.Domain;

using PlotTwist.Domain;
using PlotTwist.Domain.Movies;

public sealed class GenreTests
{
    [Theory]
    [InlineData("Sci-Fi", "science fiction")]
    [InlineData("  ANIMATION ", "animated")]
    [InlineData("animated film", "animated")]
    [InlineData("rom-com", "romantic comedy")]
    [InlineData("western", "western")]
    public void Create_normalises_case_whitespace_and_aliases(string raw, string expected) =>
        Assert.Equal(expected, Genre.Create(raw).Value);

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("-")]
    [InlineData(null)]
    public void Create_rejects_values_that_are_not_genres(string? raw) =>
        Assert.Throws<DomainException>(() => Genre.Create(raw));

    [Fact]
    public void ParseList_adds_base_genres_so_a_comedy_filter_finds_romantic_comedies() =>
        Assert.Equal(["romantic comedy", "comedy"], Genre.ParseList("romantic comedy").Select(g => g.Value));

    [Fact]
    public void ParseList_splits_on_separators_drops_unknowns_and_deduplicates() =>
        Assert.Equal(
            ["crime drama", "crime", "drama", "science fiction"],
            Genre.ParseList("Crime drama / unknown; sci-fi, drama").Select(g => g.Value));

    [Fact]
    public void Genres_are_value_objects() =>
        Assert.Equal(Genre.Create("Sci-Fi"), Genre.Create("science fiction"));
}
