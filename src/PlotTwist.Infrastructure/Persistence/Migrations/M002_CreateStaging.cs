namespace PlotTwist.Infrastructure.Persistence.Migrations;

using FluentMigrator;

// Raw source rows, all text, exactly as published: bulk-loaded with COPY before any rule runs.
// Kept between imports (replaced by the next one), so "why is this film missing?" is a query, not a re-download.
[Migration(2026101602, "Staging for raw source rows")]
public sealed class M002_CreateStaging : Migration
{
    public override void Up()
    {
        Create.Schema("staging");
        Create.Table("movies_raw").InSchema("staging")
            .WithColumn("run_id").AsInt64().NotNullable().PrimaryKey()
            .WithColumn("row_number").AsInt32().NotNullable().PrimaryKey()
            .WithColumn("release_year").AsString(int.MaxValue).NotNullable()
            .WithColumn("title").AsString(int.MaxValue).NotNullable()
            .WithColumn("origin").AsString(int.MaxValue).NotNullable()
            .WithColumn("director").AsString(int.MaxValue).NotNullable()
            .WithColumn("cast_list").AsString(int.MaxValue).NotNullable()
            .WithColumn("genre").AsString(int.MaxValue).NotNullable()
            .WithColumn("wiki_page").AsString(int.MaxValue).NotNullable()
            .WithColumn("plot").AsString(int.MaxValue).NotNullable()
            .WithColumn("plot_summary").AsString(int.MaxValue).NotNullable();
    }

    public override void Down() => Delete.Schema("staging");
}
