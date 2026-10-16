namespace PlotTwist.Application.Abstractions;

public interface IDatabaseMigrator
{
    Task<bool> MigrateUp(CancellationToken ct = default);
}
