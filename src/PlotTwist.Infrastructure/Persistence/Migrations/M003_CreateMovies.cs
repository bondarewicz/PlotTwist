namespace PlotTwist.Infrastructure.Persistence.Migrations;

using FluentMigrator;

// Films after MovieImportPolicy. Indexed for the filters the search applies:
// btree on release_year (ranges), GIN on genres (genres @> ARRAY['drama'] - a btree can't serve array containment).
[Migration(2026101603, "Movies and rejected source rows")]
public sealed class M003_CreateMovies : Migration
{
    public override void Up()
    {
        Create
            .Table("movies")
            .WithColumn("id")
            .AsGuid()
            .PrimaryKey()
            .WithColumn("source_key")
            .AsString(int.MaxValue)
            .NotNullable()
            .Unique()
            .WithColumn("title")
            .AsString(int.MaxValue)
            .NotNullable()
            .WithColumn("release_year")
            .AsInt32()
            .NotNullable()
            .Indexed()
            .WithColumn("genres")
            .AsCustom("text[]")
            .NotNullable()
            .WithColumn("origins")
            .AsCustom("text[]")
            .NotNullable()
            .WithColumn("director")
            .AsString(int.MaxValue)
            .NotNullable()
            .WithColumn("plot_summary")
            .AsString(int.MaxValue)
            .NotNullable()
            .WithColumn("wikipedia_url")
            .AsString(int.MaxValue)
            .NotNullable()
            .WithColumn("searchable_text_hash")
            .AsString(64)
            .NotNullable()
            .WithColumn("updated_at")
            .AsDateTimeOffset()
            .NotNullable()
            .WithDefault(SystemMethods.CurrentUTCDateTime);

        Execute.Sql("CREATE INDEX ix_movies_genres ON movies USING gin (genres)");

        Create
            .Table("movie_rejects")
            .WithColumn("run_id")
            .AsInt64()
            .NotNullable()
            .PrimaryKey()
            .ForeignKey("ingestion_runs", "id")
            .OnDelete(System.Data.Rule.Cascade)
            .WithColumn("row_number")
            .AsInt32()
            .NotNullable()
            .PrimaryKey()
            .WithColumn("title")
            .AsString(int.MaxValue)
            .NotNullable()
            .WithColumn("release_year")
            .AsString(int.MaxValue)
            .NotNullable()
            .WithColumn("reason")
            .AsString(int.MaxValue)
            .NotNullable();
    }

    public override void Down()
    {
        Delete.Table("movie_rejects");
        Delete.Table("movies");
    }
}
