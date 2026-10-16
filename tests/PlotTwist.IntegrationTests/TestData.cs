namespace PlotTwist.IntegrationTests;

using Domain.Embeddings;
using Domain.Movies;

internal static class TestData
{
    public static readonly EmbeddingModel Small = EmbeddingModel.Create("test-small", 3);
    public static readonly EmbeddingModel Large = EmbeddingModel.Create("test-large", 4);

    public static Movie Movie(
        string title,
        int year = 1990,
        string genres = "drama",
        string summary = "Something happens.",
        params string[] origins
    ) =>
        new(
            WikipediaPage.Create(new Uri($"https://en.wikipedia.org/wiki/{title.Replace(' ', '_')}")),
            title,
            ReleaseYear.Create(year),
            Genre.ParseList(genres),
            origins.Length == 0 ? ["American"] : origins,
            "Someone",
            summary
        );

    public static MovieEmbedding Embedding(Movie movie, EmbeddingModel model, params float[] vector) =>
        new(movie.Id, model, movie.SearchableTextHash, new Embedding(vector));
}
