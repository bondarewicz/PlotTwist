namespace PlotTwist.Infrastructure.Persistence;

using System.Runtime.CompilerServices;
using Microsoft.Extensions.VectorData;
using Npgsql;
using PlotTwist.Application.Abstractions;
using PlotTwist.Domain.Embeddings;
using PlotTwist.Domain.Movies;
using PlotTwist.Domain.Search;

// Similarity (pgvector <=> with the HNSW index) and criteria (SQL WHERE) run as one query in Postgres.
public sealed class PgVectorMovieRepository(VectorStoreCollection<int, MovieRecord> collection, NpgsqlDataSource dataSource)
    : IMovieRepository, IStorageInitializer
{
    public Task EnsureCreated(CancellationToken ct = default) => collection.EnsureCollectionExistsAsync(ct);

    public Task Save(IReadOnlyCollection<Movie> movies, CancellationToken ct = default) =>
        collection.UpsertAsync(movies.Select(MovieRecordMapper.ToRecord), ct);

    public async Task<int> Count(CancellationToken ct = default)
    {
        if (!await collection.CollectionExistsAsync(ct))
            return 0;

        await using var command = dataSource.CreateCommand($"SELECT count(*) FROM \"{collection.Name}\"");
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    public async Task<IReadOnlyList<MovieMatch>> FindSimilar(
        Embedding plot,
        MovieCriteria criteria,
        int top,
        CancellationToken ct = default)
    {
        var options = new VectorSearchOptions<MovieRecord> { Filter = MovieCriteriaFilter.ToExpression(criteria) };
        var matches = new List<MovieMatch>(top);
        await foreach (var result in collection.SearchAsync(plot.Values, top, options, ct))
            matches.Add(new MovieMatch(MovieRecordMapper.ToDomain(result.Record), result.Score ?? 0));
        return matches;
    }

    public async IAsyncEnumerable<Movie> ListEmbedded([EnumeratorCancellation] CancellationToken ct = default)
    {
        var options = new FilteredRecordRetrievalOptions<MovieRecord> { IncludeVectors = true };
        await foreach (var record in collection.GetAsync(_ => true, int.MaxValue, options, ct))
            yield return MovieRecordMapper.ToDomain(record);
    }
}
