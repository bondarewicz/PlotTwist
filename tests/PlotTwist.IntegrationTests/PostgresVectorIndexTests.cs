namespace PlotTwist.IntegrationTests;

using Domain.Embeddings;
using Infrastructure.Persistence;

// NameFor is a pure function, so these run without Docker or a database.
public sealed class PostgresVectorIndexTests
{
    [Fact]
    public void A_short_model_name_keeps_its_exact_existing_index_name()
    {
        var name = PostgresVectorIndex.NameFor(EmbeddingModel.Create("nomic-embed-text", 768));

        Assert.Equal("ix_movie_embeddings_hnsw_nomic_embed_text_768", name);
    }

    [Fact]
    public void A_long_model_name_is_truncated_but_keeps_its_dimensions_suffix()
    {
        var name = PostgresVectorIndex.NameFor(EmbeddingModel.Create(new string('a', 100) + "-embedding-model", 1536));

        Assert.True(name.Length <= 63);
        Assert.EndsWith("_1536", name);
    }

    [Fact]
    public void Two_long_names_that_truncate_to_the_same_slug_still_get_distinct_index_names()
    {
        var first = PostgresVectorIndex.NameFor(EmbeddingModel.Create(new string('a', 100) + "-one", 768));
        var second = PostgresVectorIndex.NameFor(EmbeddingModel.Create(new string('a', 100) + "-two", 768));

        Assert.NotEqual(first, second);
        Assert.True(first.Length <= 63);
        Assert.True(second.Length <= 63);
    }

    [Fact]
    public void One_long_name_at_two_dimension_sizes_still_gets_distinct_index_names()
    {
        var name = new string('a', 100) + "-embedding-model";

        var small = PostgresVectorIndex.NameFor(EmbeddingModel.Create(name, 768));
        var large = PostgresVectorIndex.NameFor(EmbeddingModel.Create(name, 1536));

        Assert.NotEqual(small, large);
        Assert.EndsWith("_768", small);
        Assert.EndsWith("_1536", large);
    }
}
