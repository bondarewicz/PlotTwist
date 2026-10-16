namespace PlotTwist.Infrastructure.Persistence;

using Application.Abstractions;
using Application.Import;
using Dapper;
using Npgsql;
using PlotTwist.Domain.Embeddings;

public sealed class PostgresIngestionRuns(NpgsqlDataSource dataSource) : IIngestionRuns, IIngestionReport
{
    public async Task<long> Start(string kind, string details, CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                "INSERT INTO ingestion_runs (kind, details) VALUES (@Kind, @Details) RETURNING id",
                new { Kind = kind, Details = details },
                cancellationToken: ct
            )
        );
    }

    public async Task Complete(long runId, IngestionCounts counts, CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE ingestion_runs
                SET status = 'completed', finished_at = now(), read = @Read, loaded = @Loaded, rejected = @Rejected, embedded = @Embedded
                WHERE id = @RunId
                """,
                new
                {
                    RunId = runId,
                    counts.Read,
                    counts.Loaded,
                    counts.Rejected,
                    counts.Embedded,
                },
                cancellationToken: ct
            )
        );
    }

    public async Task Fail(long runId, string error, IngestionCounts counts, CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE ingestion_runs
                SET status = 'failed', finished_at = now(), error = @Error, read = @Read, loaded = @Loaded, rejected = @Rejected, embedded = @Embedded
                WHERE id = @RunId
                """,
                new
                {
                    RunId = runId,
                    Error = error,
                    counts.Read,
                    counts.Loaded,
                    counts.Rejected,
                    counts.Embedded,
                },
                cancellationToken: ct
            )
        );
    }

    public async Task Cancel(long runId, IngestionCounts counts, CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE ingestion_runs
                SET status = 'cancelled', finished_at = now(), read = @Read, loaded = @Loaded, rejected = @Rejected, embedded = @Embedded
                WHERE id = @RunId
                """,
                new
                {
                    RunId = runId,
                    counts.Read,
                    counts.Loaded,
                    counts.Rejected,
                    counts.Embedded,
                },
                cancellationToken: ct
            )
        );
    }

    public async Task SaveRejects(long runId, IReadOnlyCollection<MovieReject> rejects, CancellationToken ct = default)
    {
        if (rejects.Count == 0)
            return;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO movie_rejects (run_id, row_number, title, release_year, reason)
                SELECT @RunId, * FROM unnest(@RowNumbers, @Titles, @Years, @Reasons)
                """,
                new
                {
                    RunId = runId,
                    RowNumbers = rejects.Select(r => r.RowNumber).ToArray(),
                    Titles = rejects.Select(r => r.Title).ToArray(),
                    Years = rejects.Select(r => r.ReleaseYear).ToArray(),
                    Reasons = rejects.Select(r => r.Reason).ToArray(),
                },
                cancellationToken: ct
            )
        );
    }

    public async Task<IngestionSnapshot> Get(EmbeddingModel model, CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var lastImport = await connection.QuerySingleOrDefaultAsync<LastImportRow>(
            new CommandDefinition(
                """
                SELECT id AS run_id, details, read, loaded, rejected, finished_at
                FROM ingestion_runs WHERE kind = 'import' AND status = 'completed'
                ORDER BY id DESC LIMIT 1
                """,
                cancellationToken: ct
            )
        );
        var counts = await connection.QuerySingleAsync<CountsRow>(
            new CommandDefinition(
                """
                SELECT
                    (SELECT count(*) FROM movies) AS movies,
                    (SELECT count(*) FROM movie_embeddings WHERE model = @Model) AS embedded,
                    (SELECT count(*) FROM movies m LEFT JOIN movie_embeddings e ON e.movie_id = m.id AND e.model = @Model
                     WHERE e.movie_id IS NULL OR e.text_hash <> m.searchable_text_hash) AS pending
                """,
                new { Model = model.Name },
                cancellationToken: ct
            )
        );

        return new IngestionSnapshot(
            lastImport is null
                ? null
                : new LastImport(
                    lastImport.RunId,
                    lastImport.Details,
                    lastImport.Read,
                    lastImport.Loaded,
                    lastImport.Rejected,
                    new DateTimeOffset(lastImport.FinishedAt, TimeSpan.Zero)
                ),
            (int)counts.Movies,
            (int)counts.Embedded,
            (int)counts.Pending
        );
    }

    // Npgsql reads timestamptz as a UTC DateTime.
    private sealed record LastImportRow(
        long RunId,
        string Details,
        int Read,
        int Loaded,
        int Rejected,
        DateTime FinishedAt
    );

    private sealed record CountsRow(long Movies, long Embedded, long Pending);
}
