namespace PlotTwist.Infrastructure.Persistence;

using System.Security.Cryptography;
using System.Text;
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
        await using var transaction = await connection.BeginTransactionAsync(ct);

        if (rebuild)
            await connection.ExecuteAsync(
                new CommandDefinition($"DROP INDEX IF EXISTS {name}", transaction: transaction, cancellationToken: ct)
            );

        // SET LOCAL is scoped to this transaction, so it can't leak onto other work from the same pooled
        // connection the way a session-level SET would. Enough memory to build the graph in memory, and workers.
        await connection.ExecuteAsync(
            new CommandDefinition(
                $"SET LOCAL maintenance_work_mem = '{options.IndexBuildMemory}'",
                transaction: transaction,
                cancellationToken: ct
            )
        );
        await connection.ExecuteAsync(
            new CommandDefinition(
                $"SET LOCAL max_parallel_maintenance_workers = {options.IndexBuildWorkers}",
                transaction: transaction,
                cancellationToken: ct
            )
        );
        await connection.ExecuteAsync(
            new CommandDefinition(
                $"""
                CREATE INDEX IF NOT EXISTS {name} ON movie_embeddings
                USING hnsw ((embedding::vector({model.Dimensions})) vector_cosine_ops)
                WITH (m = {options.HnswM}, ef_construction = {options.HnswEfConstruction})
                WHERE model = '{model.Name.Replace("'", "''")}'
                """,
                transaction: transaction,
                commandTimeout: 0,
                cancellationToken: ct
            )
        );

        await transaction.CommitAsync(ct);
        return await Status(model, ct);
    }

    // Identifiers can't be parameters, so the name is reduced to [a-z0-9_]. The dimensions suffix is never
    // truncated away (it's what keeps two models at different dimensions from aliasing); when the slug itself
    // would push the name past Postgres' 63 chars, it's cut short and a hash of the full model name appended so
    // two long names that happen to share a prefix still get distinct indexes.
    internal static string NameFor(EmbeddingModel model)
    {
        const string prefix = "ix_movie_embeddings_hnsw_";
        var slug = NonIdentifier().Replace(model.Name.ToLowerInvariant(), "_").Trim('_');
        var suffix = $"_{model.Dimensions}";
        var name = $"{prefix}{slug}{suffix}";
        if (name.Length <= 63)
            return name;

        var hash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{model.Name}:{model.Dimensions}"))
        )[..8];
        var budget = Math.Max(63 - prefix.Length - suffix.Length - hash.Length - 1, 0);
        var truncated = slug[..Math.Min(slug.Length, budget)].TrimEnd('_');
        return $"{prefix}{truncated}_{hash}{suffix}";
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonIdentifier();
}
