namespace PlotTwist.Infrastructure.Persistence;

using Application.Abstractions;
using FluentMigrator.Runner;

public sealed class PostgresDatabaseMigrator(IMigrationRunner runner) : IDatabaseMigrator
{
    public Task<bool> MigrateUp(CancellationToken ct = default)
    {
        var pending = runner.HasMigrationsToApplyUp();
        if (pending)
            runner.MigrateUp();
        return Task.FromResult(pending);
    }
}
