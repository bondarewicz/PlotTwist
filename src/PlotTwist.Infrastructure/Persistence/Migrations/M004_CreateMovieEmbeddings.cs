namespace PlotTwist.Infrastructure.Persistence.Migrations;

using FluentMigrator;

// Embeddings are derived data, one row per (movie, model): several models can live side by side, which is how a
// new model is embedded and evaluated before the Api switches to it. The column is an untyped `vector` so models
// with different dimensions share the table; each model gets its own partial HNSW index over
// (embedding::vector(N)), built by the `index` step once its vectors are loaded (see PostgresVectorIndex).
[Migration(2026101604, "Movie embeddings per model")]
public sealed class M004_CreateMovieEmbeddings : Migration
{
    public override void Up()
    {
        Execute.Sql("CREATE EXTENSION IF NOT EXISTS vector");

        Create.Table("movie_embeddings")
            .WithColumn("movie_id").AsGuid().NotNullable().PrimaryKey().ForeignKey("movies", "id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("model").AsString(200).NotNullable().PrimaryKey()
            .WithColumn("dimensions").AsInt32().NotNullable()
            .WithColumn("text_hash").AsString(64).NotNullable()
            .WithColumn("embedding").AsCustom("vector").NotNullable()
            .WithColumn("created_at").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);
    }

    public override void Down() => Delete.Table("movie_embeddings");
}
