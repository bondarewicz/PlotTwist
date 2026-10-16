namespace PlotTwist.Infrastructure.Persistence;

using Dapper;
using Domain.Movies;
using Npgsql;
using NpgsqlTypes;

public sealed class PostgresMovieRepository(NpgsqlDataSource dataSource) : IMovieRepository
{
    // xmax = 0 only for freshly inserted rows: counts new movies without a second query.
    private const string UpsertSql = """
        WITH upserted AS (
            INSERT INTO movies (id, source_key, title, release_year, genres, origins, director, plot_summary, wikipedia_url, searchable_text_hash)
            SELECT id, source_key, title, release_year, genres, origins, director, plot_summary, wikipedia_url, searchable_text_hash FROM movies_batch
            ON CONFLICT (id) DO UPDATE SET
                title = excluded.title, release_year = excluded.release_year, genres = excluded.genres,
                origins = excluded.origins, director = excluded.director, plot_summary = excluded.plot_summary,
                wikipedia_url = excluded.wikipedia_url, searchable_text_hash = excluded.searchable_text_hash,
                updated_at = now()
            WHERE movies.searchable_text_hash IS DISTINCT FROM excluded.searchable_text_hash
               OR movies.origins IS DISTINCT FROM excluded.origins
               OR movies.director IS DISTINCT FROM excluded.director
            RETURNING (xmax = 0) AS inserted
        )
        SELECT count(*) FILTER (WHERE inserted) FROM upserted
        """;

    public async Task<int> Upsert(IReadOnlyCollection<Movie> movies, CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        await connection.ExecuteAsync(
            new CommandDefinition(
                "CREATE TEMP TABLE movies_batch (LIKE movies INCLUDING DEFAULTS) ON COMMIT DROP",
                transaction: transaction,
                cancellationToken: ct
            )
        );
        await CopyMovies(connection, movies, ct);
        var inserted = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(UpsertSql, transaction: transaction, cancellationToken: ct)
        );

        await transaction.CommitAsync(ct);
        return inserted;
    }

    private static async Task CopyMovies(
        NpgsqlConnection connection,
        IReadOnlyCollection<Movie> movies,
        CancellationToken ct
    )
    {
        await using var writer = await connection.BeginBinaryImportAsync(
            "COPY movies_batch (id, source_key, title, release_year, genres, origins, director, plot_summary, wikipedia_url, searchable_text_hash) FROM STDIN (FORMAT BINARY)",
            ct
        );
        foreach (var movie in movies)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(movie.Id.Value, NpgsqlDbType.Uuid, ct);
            await writer.WriteAsync(movie.WikipediaPage.Key, NpgsqlDbType.Text, ct);
            await writer.WriteAsync(movie.Title, NpgsqlDbType.Text, ct);
            await writer.WriteAsync(movie.ReleaseYear.Value, NpgsqlDbType.Integer, ct);
            await writer.WriteAsync(
                movie.Genres.Select(g => g.Value).ToArray(),
                NpgsqlDbType.Array | NpgsqlDbType.Text,
                ct
            );
            await writer.WriteAsync(movie.Origins.ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Text, ct);
            await writer.WriteAsync(movie.Director, NpgsqlDbType.Text, ct);
            await writer.WriteAsync(movie.PlotSummary, NpgsqlDbType.Text, ct);
            await writer.WriteAsync(movie.WikipediaPage.Url.ToString(), NpgsqlDbType.Text, ct);
            await writer.WriteAsync(movie.SearchableTextHash, NpgsqlDbType.Text, ct);
        }

        await writer.CompleteAsync(ct);
    }

    public async Task<IReadOnlyList<Movie>> Get(IReadOnlyCollection<MovieId> ids, CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<MovieRow>(
            new CommandDefinition(
                $"SELECT {MovieRow.Columns} FROM movies m WHERE m.id = ANY(@Ids)",
                new { Ids = ids.Select(i => i.Value).ToArray() },
                cancellationToken: ct
            )
        );
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<int> Count(CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT count(*) FROM movies", cancellationToken: ct)
        );
    }
}
