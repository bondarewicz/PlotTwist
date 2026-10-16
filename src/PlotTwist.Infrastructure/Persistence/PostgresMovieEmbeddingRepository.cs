namespace PlotTwist.Infrastructure.Persistence;

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Dapper;
using Domain.Movies;
using Domain.Search;
using Npgsql;
using NpgsqlTypes;
using Pgvector;
using PlotTwist.Domain.Embeddings;
using DomainEmbedding = PlotTwist.Domain.Embeddings.Embedding;

public sealed class PostgresMovieEmbeddingRepository(NpgsqlDataSource dataSource) : IMovieEmbeddingRepository
{
    public async Task<IReadOnlyList<MovieId>> ListPending(EmbeddingModel model, CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var ids = await connection.QueryAsync<Guid>(
            new CommandDefinition(
                """
                SELECT m.id
                FROM movies m
                LEFT JOIN movie_embeddings e ON e.movie_id = m.id AND e.model = @Model
                WHERE e.movie_id IS NULL OR e.text_hash <> m.searchable_text_hash
                ORDER BY m.id
                """,
                new { Model = model.Name },
                cancellationToken: ct
            )
        );
        return ids.Select(MovieId.FromValue).ToList();
    }

    public async Task Save(IReadOnlyCollection<MovieEmbedding> embeddings, CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        await connection.ExecuteAsync(
            new CommandDefinition(
                "CREATE TEMP TABLE embeddings_batch (LIKE movie_embeddings INCLUDING DEFAULTS) ON COMMIT DROP",
                transaction: transaction,
                cancellationToken: ct
            )
        );

        await using (
            var writer = await connection.BeginBinaryImportAsync(
                "COPY embeddings_batch (movie_id, model, dimensions, text_hash, embedding) FROM STDIN (FORMAT BINARY)",
                ct
            )
        )
        {
            foreach (var embedding in embeddings)
            {
                await writer.StartRowAsync(ct);
                await writer.WriteAsync(embedding.MovieId.Value, NpgsqlDbType.Uuid, ct);
                await writer.WriteAsync(embedding.Model.Name, NpgsqlDbType.Text, ct);
                await writer.WriteAsync(embedding.Model.Dimensions, NpgsqlDbType.Integer, ct);
                await writer.WriteAsync(embedding.TextHash, NpgsqlDbType.Text, ct);
                await writer.WriteAsync(new Vector(embedding.Vector.Values), "vector", ct);
            }

            await writer.CompleteAsync(ct);
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO movie_embeddings (movie_id, model, dimensions, text_hash, embedding)
                SELECT movie_id, model, dimensions, text_hash, embedding FROM embeddings_batch
                ON CONFLICT (movie_id, model) DO UPDATE SET
                    dimensions = excluded.dimensions, text_hash = excluded.text_hash, embedding = excluded.embedding
                """,
                transaction: transaction,
                cancellationToken: ct
            )
        );

        await transaction.CommitAsync(ct);
    }

    public async Task<int> Count(EmbeddingModel model, CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT count(*) FROM movie_embeddings WHERE model = @Model",
                new { Model = model.Name },
                cancellationToken: ct
            )
        );
    }

    // The ORDER BY expression must be exactly the partial index's expression - (embedding::vector(N)) with
    // cosine distance, and model = '...' in the WHERE - or Postgres falls back to scanning every row.
    // N comes from validated configuration, never from input.
    public async Task<IReadOnlyList<MovieMatch>> FindSimilar(
        DomainEmbedding plot,
        EmbeddingModel model,
        MovieCriteria criteria,
        int top,
        CancellationToken ct = default
    )
    {
        var sql = new StringBuilder(
            string.Create(
                CultureInfo.InvariantCulture,
                $"""
                SELECT {MovieRow.Columns}, 1 - ((e.embedding::vector({model.Dimensions})) <=> @Query::vector({model.Dimensions})) AS similarity
                FROM movie_embeddings e
                JOIN movies m ON m.id = e.movie_id
                WHERE e.model = @Model
                """
            )
        );
        if (criteria.ReleasedFrom is not null)
            sql.Append(" AND m.release_year >= @From");
        if (criteria.ReleasedTo is not null)
            sql.Append(" AND m.release_year <= @To");
        if (criteria.Genre is not null)
            sql.Append(" AND m.genres @> ARRAY[@Genre]");
        sql.Append(
            CultureInfo.InvariantCulture,
            $"""

            ORDER BY (e.embedding::vector({model.Dimensions})) <=> @Query::vector({model.Dimensions})
            LIMIT @Top
            """
        );

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<SimilarRow>(
            new CommandDefinition(
                sql.ToString(),
                new
                {
                    Query = new Vector(plot.Values),
                    Model = model.Name,
                    From = criteria.ReleasedFrom?.Value,
                    To = criteria.ReleasedTo?.Value,
                    Genre = criteria.Genre?.Value,
                    Top = top,
                },
                cancellationToken: ct
            )
        );
        return rows.Select(r => new MovieMatch(r.ToDomain(), r.Similarity)).ToList();
    }

    public async IAsyncEnumerable<(Movie Movie, DomainEmbedding Vector)> ListAll(
        EmbeddingModel model,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        // Dapper's public QueryUnbufferedAsync has no CommandDefinition overload, so ct only cancels enumeration
        // (WithCancellation below), not the command itself - the widest cancellation this API allows.
        var rows = connection.QueryUnbufferedAsync<EmbeddedRow>(
            $"SELECT {MovieRow.Columns}, e.embedding FROM movie_embeddings e JOIN movies m ON m.id = e.movie_id WHERE e.model = @Model",
            new { Model = model.Name }
        );
        await foreach (var row in rows.WithCancellation(ct))
            yield return (row.ToDomain(), new DomainEmbedding(row.Embedding.Memory.Span));
    }

    private sealed class SimilarRow
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = "";
        public int ReleaseYear { get; init; }
        public string[] Genres { get; init; } = [];
        public string[] Origins { get; init; } = [];
        public string Director { get; init; } = "";
        public string PlotSummary { get; init; } = "";
        public string WikipediaUrl { get; init; } = "";
        public double Similarity { get; init; }

        public Movie ToDomain() =>
            new MovieRow
            {
                Id = Id,
                Title = Title,
                ReleaseYear = ReleaseYear,
                Genres = Genres,
                Origins = Origins,
                Director = Director,
                PlotSummary = PlotSummary,
                WikipediaUrl = WikipediaUrl,
            }.ToDomain();
    }

    private sealed class EmbeddedRow
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = "";
        public int ReleaseYear { get; init; }
        public string[] Genres { get; init; } = [];
        public string[] Origins { get; init; } = [];
        public string Director { get; init; } = "";
        public string PlotSummary { get; init; } = "";
        public string WikipediaUrl { get; init; } = "";
        public Vector Embedding { get; init; } = null!;

        public Movie ToDomain() =>
            new MovieRow
            {
                Id = Id,
                Title = Title,
                ReleaseYear = ReleaseYear,
                Genres = Genres,
                Origins = Origins,
                Director = Director,
                PlotSummary = PlotSummary,
                WikipediaUrl = WikipediaUrl,
            }.ToDomain();
    }
}
