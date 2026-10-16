namespace PlotTwist.Infrastructure.Persistence;

using FluentMigrator.Runner;
using PlotTwist.Application.Abstractions;

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
