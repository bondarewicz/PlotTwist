namespace PlotTwist.Infrastructure.Persistence.Migrations;

using FluentMigrator;

// The `vector` column is untyped so models of different dimensions can share this table.
[Migration(202610161255, "Movie embeddings per model")]
public sealed class CreateMovieEmbeddings : Migration
{
    public override void Up()
    {
        Execute.Sql("CREATE EXTENSION IF NOT EXISTS vector");

        Create
            .Table("movie_embeddings")
            .WithColumn("movie_id")
            .AsGuid()
            .NotNullable()
            .PrimaryKey()
            .ForeignKey("movies", "id")
            .OnDelete(System.Data.Rule.Cascade)
            .WithColumn("model")
            .AsString(200)
            .NotNullable()
            .PrimaryKey()
            .WithColumn("dimensions")
            .AsInt32()
            .NotNullable()
            .WithColumn("text_hash")
            .AsString(64)
            .NotNullable()
            .WithColumn("embedding")
            .AsCustom("vector")
            .NotNullable()
            .WithColumn("created_at")
            .AsDateTimeOffset()
            .NotNullable()
            .WithDefault(SystemMethods.CurrentUTCDateTime);
    }

    public override void Down() => Delete.Table("movie_embeddings");
}
