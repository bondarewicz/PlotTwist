namespace PlotTwist.Application.Abstractions;

public interface IDatabaseMigrator
{
    // Applies pending schema migrations; returns whether any were applied.
    Task<bool> MigrateUp(CancellationToken ct = default);
}
