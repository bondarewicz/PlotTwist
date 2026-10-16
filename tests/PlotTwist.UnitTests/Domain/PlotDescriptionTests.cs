namespace PlotTwist.UnitTests.Domain;

using PlotTwist.Domain;
using PlotTwist.Domain.Search;

public sealed class PlotDescriptionTests
{
    [Fact]
    public void Whitespace_runs_collapse_and_case_is_kept() =>
        Assert.Equal("A man on Mars", PlotDescription.Create("  A man \n on\tMars ").Value);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_descriptions_are_rejected(string? raw) =>
        Assert.Throws<DomainException>(() => PlotDescription.Create(raw));

    [Fact]
    public void Descriptions_over_the_limit_are_rejected() =>
        Assert.Throws<DomainException>(() => PlotDescription.Create(new string('x', PlotDescription.MaxLength + 1)));
}
