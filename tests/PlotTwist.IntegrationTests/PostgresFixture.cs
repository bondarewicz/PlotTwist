namespace PlotTwist.IntegrationTests;

using Dapper;
using FluentMigrator.Runner;
using Infrastructure.Configuration;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();

    public NpgsqlDataSource DataSource { get; private set; } = null!;

    public DatabaseOptions Database { get; } =
        new()
        {
            HnswM = 16,
            HnswEfConstruction = 64,
            IndexBuildMemory = "64MB",
            IndexBuildWorkers = 0,
        };

    public string ConnectionString =>
        container.GetConnectionString() + ";Options=-c hnsw.iterative_scan=relaxed_order -c hnsw.ef_search=100";

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();
        Migrate();

        var builder = new NpgsqlDataSourceBuilder(ConnectionString);
        builder.UseVector();
        DataSource = builder.Build();
    }

    public bool Migrate()
    {
        using var services = new ServiceCollection()
            .AddFluentMigratorCore()
            .ConfigureRunner(runner =>
                runner
                    .AddPostgres()
                    .WithGlobalConnectionString(ConnectionString)
                    .ScanIn(typeof(CreateIngestionRuns).Assembly)
                    .For.Migrations()
            )
            .BuildServiceProvider();
        using var scope = services.CreateScope();
        return new PostgresDatabaseMigrator(scope.ServiceProvider.GetRequiredService<IMigrationRunner>())
            .MigrateUp()
            .GetAwaiter()
            .GetResult();
    }

    public async Task Reset()
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(
            "TRUNCATE movie_embeddings, movie_rejects, movies, ingestion_runs, staging.movies_raw RESTART IDENTITY CASCADE"
        );
        foreach (
            var index in await connection.QueryAsync<string>(
                "SELECT indexname FROM pg_indexes WHERE indexname LIKE 'ix_movie_embeddings_hnsw_%'"
            )
        )
            await connection.ExecuteAsync($"DROP INDEX {index}");
    }

    public async ValueTask DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
