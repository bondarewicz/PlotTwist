namespace PlotTwist.UnitTests.Application;

using PlotTwist.Application.Import;
using static Movies;

public sealed class MovieImportPolicyTests
{
    [Fact]
    public void One_row_per_page_becomes_one_film()
    {
        var decision = MovieImportPolicy.Decide([
            Row(1, "Alien", "1979", "https://en.wikipedia.org/wiki/Alien_(film)"),
        ]);

        Assert.Equal("Alien", Assert.Single(decision.Movies).Title);
        Assert.Empty(decision.Rejects);
    }

    [Fact]
    public void A_co_production_listed_per_country_becomes_one_film_with_both_origins()
    {
        var decision = MovieImportPolicy.Decide([
            Row(
                1,
                "The Outsider",
                "1939",
                "https://en.wikipedia.org/wiki/The_Outsider_(1939_film)",
                origin: "American"
            ),
            Row(2, "The Outsider", "1939", "https://en.wikipedia.org/wiki/The_Outsider_(1939_film)", origin: "British"),
        ]);

        Assert.Equal(["American", "British"], Assert.Single(decision.Movies).Origins);
        Assert.Empty(decision.Rejects);
    }

    [Fact]
    public void Different_films_on_a_page_that_names_its_year_keep_the_named_one_and_reject_the_other()
    {
        var decision = MovieImportPolicy.Decide([
            Row(1, "Titanic", "1953", "https://en.wikipedia.org/wiki/Titanic_(1997_film)"),
            Row(2, "Titanic", "1997", "https://en.wikipedia.org/wiki/Titanic_(1997_film)"),
        ]);

        Assert.Equal(1997, Assert.Single(decision.Movies).ReleaseYear.Value);
        var reject = Assert.Single(decision.Rejects);
        Assert.Equal(1, reject.RowNumber);
        Assert.Contains("whose year the page names", reject.Reason);
    }

    [Fact]
    public void Different_films_on_a_page_that_names_no_year_are_all_rejected_because_the_plot_cant_be_attributed()
    {
        var decision = MovieImportPolicy.Decide([
            Row(1, "The Other Side of the Door", "1916", "https://en.wikipedia.org/wiki/The_Other_Side_of_the_Door"),
            Row(2, "The Other Side of the Door", "2016", "https://en.wikipedia.org/wiki/The_Other_Side_of_the_Door"),
        ]);

        Assert.Empty(decision.Movies);
        Assert.Equal([1, 2], decision.Rejects.Select(r => r.RowNumber));
        Assert.All(decision.Rejects, r => Assert.Contains("can't be attributed", r.Reason));
    }

    [Fact]
    public void Pages_are_compared_without_fragment_trailing_slash_or_case()
    {
        var decision = MovieImportPolicy.Decide([
            Row(1, "Up", "2009", "https://en.wikipedia.org/wiki/Up_(2009_film)"),
            Row(2, "Up", "2009", "https://en.wikipedia.org/wiki/up_(2009_film)/#Plot", origin: "British"),
        ]);

        Assert.Single(decision.Movies);
    }

    [Theory]
    [InlineData("19x5", "is not a year")]
    [InlineData("1700", "outside")]
    public void Rows_breaking_domain_rules_are_rejected_with_the_rule(string year, string reason)
    {
        var decision = MovieImportPolicy.Decide([Row(7, "Broken", year, "https://en.wikipedia.org/wiki/Broken")]);

        Assert.Empty(decision.Movies);
        Assert.Contains(reason, Assert.Single(decision.Rejects).Reason);
    }

    [Fact]
    public void Rows_without_a_page_are_rejected()
    {
        var decision = MovieImportPolicy.Decide([Row(3, "Nowhere", "1990", "")]);

        Assert.Contains("no identity", Assert.Single(decision.Rejects).Reason);
    }

    [Fact]
    public void Every_row_is_either_part_of_a_film_or_rejected()
    {
        RawMovieRowSet rows =
        [
            Row(1, "A", "1990", "https://en.wikipedia.org/wiki/A"),
            Row(2, "A", "1990", "https://en.wikipedia.org/wiki/A", origin: "British"),
            Row(3, "B", "1991", "https://en.wikipedia.org/wiki/B"),
            Row(4, "C", "1992", "https://en.wikipedia.org/wiki/B"),
            Row(5, "D", "nope", "https://en.wikipedia.org/wiki/D"),
        ];

        var decision = MovieImportPolicy.Decide(rows);

        Assert.Equal(rows.Count, decision.Movies.Sum(m => m.Origins.Count) + decision.Rejects.Count);
    }

    private sealed class RawMovieRowSet : List<PlotTwist.Application.Abstractions.RawMovieRow>;
}
