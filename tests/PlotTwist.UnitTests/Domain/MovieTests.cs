namespace PlotTwist.UnitTests.Domain;

using PlotTwist.Domain;
using PlotTwist.Domain.Embeddings;
using PlotTwist.Domain.Movies;
using PlotTwist.Domain.Search;

public sealed class MovieTests
{
    [Fact]
    public void A_movie_without_a_plot_summary_cannot_be_searched_so_it_is_rejected() =>
        Assert.Throws<DomainException>(() => Movies.Create(summary: " "));

    [Fact]
    public void Searchable_text_combines_title_year_genres_and_plot() =>
        Assert.Equal(
            "Alien (1979). science fiction, horror. A crew meets a creature.",
            Movies.Create("Alien", 1979, "sci-fi, horror", "A crew meets a creature.").SearchableText);

    [Fact]
    public void A_movie_is_embedded_once_an_embedding_is_assigned()
    {
        var movie = Movies.Create();
        movie.AssignPlotEmbedding(new Embedding([1f, 0f]));
        Assert.True(movie.IsEmbedded);
    }

    [Theory]
    [InlineData(1887)]
    [InlineData(2101)]
    public void Release_years_outside_film_history_are_rejected(int year) =>
        Assert.Throws<DomainException>(() => ReleaseYear.Create(year));

    [Fact]
    public void Ambiguous_when_the_runner_up_is_within_the_margin()
    {
        MovieMatch[] matches = [new(Movies.Create(), 0.80), new(Movies.Create(), 0.79)];
        Assert.True(MatchConfidence.IsAmbiguous(matches));
    }

    [Fact]
    public void Not_ambiguous_when_the_best_match_stands_out()
    {
        MovieMatch[] matches = [new(Movies.Create(), 0.80), new(Movies.Create(), 0.70)];
        Assert.False(MatchConfidence.IsAmbiguous(matches));
    }
}
