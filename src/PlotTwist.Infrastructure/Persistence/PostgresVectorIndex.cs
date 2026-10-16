namespace PlotTwist.Infrastructure.Persistence;

using System.Text.RegularExpressions;
using Application.Abstractions;
using Configuration;
using Dapper;
using Npgsql;
using PlotTwist.Domain.Embeddings;

// One partial HNSW index per model: (embedding::vector(N)) with cosine distance, WHERE model = '...'.
// This is the pgvector-documented way to index several models with different dimensions in one column.
// It's operational DDL driven by configuration (which model, which dimensions), so it lives here rather than in a
// migration, and runs after a bulk load: building once over 35k vectors is far faster than growing it row by row.
public sealed partial class PostgresVectorIndex(NpgsqlDataSource dataSource, DatabaseOptions options) : IVectorIndex
{
    public async Task<VectorIndexStatus> Status(EmbeddingModel model, CancellationToken ct = default)
    {
        var name = NameFor(model);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var size = await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                """
                SELECT pg_size_pretty(pg_relation_size(c.oid)) FROM pg_class c WHERE c.relname = @Name AND c.relkind = 'i'
                """,
                new { Name = name },
                cancellationToken: ct
            )
        );
        return new VectorIndexStatus(name, size is not null, size);
    }

    public async Task<VectorIndexStatus> Build(EmbeddingModel model, bool rebuild, CancellationToken ct = default)
    {
        var name = NameFor(model);
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        if (rebuild)
            await connection.ExecuteAsync($"DROP INDEX IF EXISTS {name}");

        // Session settings for the build only: enough memory to build the graph in memory, and parallel workers.
        await connection.ExecuteAsync($"SET maintenance_work_mem = '{options.IndexBuildMemory}'");
        await connection.ExecuteAsync($"SET max_parallel_maintenance_workers = {options.IndexBuildWorkers}");
        await connection.ExecuteAsync(
            new CommandDefinition(
                $"""
                CREATE INDEX IF NOT EXISTS {name} ON movie_embeddings
                USING hnsw ((embedding::vector({model.Dimensions})) vector_cosine_ops)
                WITH (m = {options.HnswM}, ef_construction = {options.HnswEfConstruction})
                WHERE model = '{model.Name.Replace("'", "''")}'
                """,
                commandTimeout: 0,
                cancellationToken: ct
            )
        );

        return await Status(model, ct);
    }

    // Identifiers can't be parameters, so the name is reduced to [a-z0-9_] and capped below Postgres' 63 chars.
    internal static string NameFor(EmbeddingModel model)
    {
        var slug = NonIdentifier().Replace(model.Name.ToLowerInvariant(), "_").Trim('_');
        var name = $"ix_movie_embeddings_hnsw_{slug}_{model.Dimensions}";
        return name.Length <= 63 ? name : name[..63];
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonIdentifier();
}
