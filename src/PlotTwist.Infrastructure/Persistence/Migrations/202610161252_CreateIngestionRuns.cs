namespace PlotTwist.Infrastructure.Persistence.Migrations;

using FluentMigrator;

[Migration(202610161252, "Ingestion run log")]
public sealed class CreateIngestionRuns : Migration
{
    public override void Up() =>
        Create
            .Table("ingestion_runs")
            .WithColumn("id")
            .AsInt64()
            .PrimaryKey()
            .Identity()
            .WithColumn("kind")
            .AsString(20)
            .NotNullable()
            .WithColumn("details")
            .AsString(int.MaxValue)
            .NotNullable()
            .WithColumn("status")
            .AsString(20)
            .NotNullable()
            .WithDefaultValue("running")
            .WithColumn("started_at")
            .AsDateTimeOffset()
            .NotNullable()
            .WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("finished_at")
            .AsDateTimeOffset()
            .Nullable()
            .WithColumn("read")
            .AsInt32()
            .NotNullable()
            .WithDefaultValue(0)
            .WithColumn("loaded")
            .AsInt32()
            .NotNullable()
            .WithDefaultValue(0)
            .WithColumn("rejected")
            .AsInt32()
            .NotNullable()
            .WithDefaultValue(0)
            .WithColumn("embedded")
            .AsInt32()
            .NotNullable()
            .WithDefaultValue(0)
            .WithColumn("error")
            .AsString(int.MaxValue)
            .Nullable();

    public override void Down() => Delete.Table("ingestion_runs");
}
