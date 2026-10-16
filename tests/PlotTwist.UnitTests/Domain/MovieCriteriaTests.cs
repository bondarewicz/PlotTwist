namespace PlotTwist.UnitTests.Domain;

using PlotTwist.Domain;
using PlotTwist.Domain.Search;

public sealed class MovieCriteriaTests
{
    [Fact]
    public void No_values_means_no_criteria() => Assert.True(MovieCriteria.Create().IsEmpty);

    [Fact]
    public void A_year_range_the_wrong_way_round_is_rejected() =>
        Assert.Throws<DomainException>(() => MovieCriteria.Create(releasedFrom: 2000, releasedTo: 1990));

    [Fact]
    public void Years_outside_film_history_are_rejected() =>
        Assert.Throws<DomainException>(() => MovieCriteria.Create(releasedFrom: 1700));

    [Fact]
    public void Blank_genre_is_treated_as_no_genre() => Assert.Null(MovieCriteria.Create(genre: "  ").Genre);

    [Theory]
    [InlineData(1995, "drama", true)]
    [InlineData(1985, "drama", false)]
    [InlineData(1995, "comedy", false)]
    public void IsSatisfiedBy_applies_every_criterion(int year, string genre, bool expected)
    {
        var criteria = MovieCriteria.Create(1990, 1999, "drama");
        Assert.Equal(expected, criteria.IsSatisfiedBy(Movies.Create(year: year, genres: genre)));
    }
}
