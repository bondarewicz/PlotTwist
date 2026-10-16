namespace PlotTwist.IntegrationTests;

using Testcontainers.PostgreSql;

// A throwaway pgvector Postgres per test run (needs Docker). Same image as docker-compose.yml.
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();

    // Same session settings the app uses (see DatabaseOptions).
    public string ConnectionString => container.GetConnectionString() + ";Options=-c hnsw.iterative_scan=relaxed_order -c hnsw.ef_search=100";

    public ValueTask InitializeAsync() => new(container.StartAsync());

    public ValueTask DisposeAsync() => container.DisposeAsync();
}
