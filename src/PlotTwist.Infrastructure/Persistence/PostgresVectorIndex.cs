namespace PlotTwist.Infrastructure.Persistence;

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Application.Abstractions;
using Configuration;
using Dapper;
using Npgsql;
using PlotTwist.Domain.Embeddings;

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

        // SET LOCAL is scoped to this transaction, so it can't leak onto other work from the same pooled connection.
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

    // Postgres identifiers cap at 63 chars; a hash of the full name is appended so a truncated slug stays distinct.
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
