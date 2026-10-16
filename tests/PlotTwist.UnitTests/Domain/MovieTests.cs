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
            Movies.Create("Alien", 1979, "sci-fi, horror", "A crew meets a creature.").SearchableText
        );

    [Fact]
    public void Identity_comes_from_the_wikipedia_page_so_re_imports_find_the_same_film()
    {
        var first = Movies.Create(title: "Alien", url: "https://en.wikipedia.org/wiki/Alien_(film)");
        var again = Movies.Create(title: "Alien (restored)", url: "https://EN.wikipedia.org/wiki/alien_(film)/");

        Assert.Equal(first.Id, again.Id);
    }

    [Fact]
    public void The_text_hash_changes_exactly_when_the_searchable_text_does()
    {
        var movie = Movies.Create(summary: "A crew meets a creature.", url: "https://en.wikipedia.org/wiki/Alien");

        Assert.Equal(
            movie.SearchableTextHash,
            Movies
                .Create(
                    summary: "A crew meets a creature.",
                    url: "https://en.wikipedia.org/wiki/Alien",
                    origin: "British"
                )
                .SearchableTextHash
        );
        Assert.NotEqual(
            movie.SearchableTextHash,
            Movies
                .Create(summary: "A crew meets a monster.", url: "https://en.wikipedia.org/wiki/Alien")
                .SearchableTextHash
        );
    }

    [Fact]
    public void An_embedding_must_fit_its_model() =>
        Assert.Throws<DomainException>(() =>
            new MovieEmbedding(Movies.Create().Id, EmbeddingModel.Create("m", 3), "hash", new Embedding([1f, 2f]))
        );

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
