using System.Linq.Expressions;
using CommunityToolkit.VectorData.PgVector;
using Microsoft.Extensions.VectorData;
using PlotTwist.Core.Movies;
using PlotTwist.Core.Ollama;

namespace PlotTwist.Core.Search;

public sealed record MovieFilter(int? MinYear = null, int? MaxYear = null, string? Genre = null)
{
    public static readonly MovieFilter None = new();

    public bool IsEmpty => MinYear is null && MaxYear is null && string.IsNullOrWhiteSpace(Genre);

    public override string ToString() => IsEmpty
        ? "(none)"
        : string.Join(", ", new[]
        {
            MinYear is { } min ? $"year >= {min}" : null,
            MaxYear is { } max ? $"year <= {max}" : null,
            string.IsNullOrWhiteSpace(Genre) ? null : $"genre = {Genres.Normalize(Genre)}",
        }.OfType<string>());
}

public sealed record MovieHit(MoviePlot Movie, double Score);

/// <summary>
/// Vector search in Postgres: the cosine comparison and the HNSW index both live in pgvector,
/// and the metadata filter becomes a plain SQL WHERE clause on the same query.
/// </summary>
public sealed class MovieSearch(VectorStoreCollection<int, MoviePlot> collection, MovieEmbedder embedder)
{
    public VectorStoreCollection<int, MoviePlot> Collection => collection;
    public MovieEmbedder Embedder => embedder;

    public static MovieSearch Create(PlotTwistSettings settings)
    {
        var store = new PostgresVectorStore(settings.PostgresConnectionString);
        var collection = store.GetCollection<int, MoviePlot>(settings.Collection);
        var embedder = new MovieEmbedder(OllamaFactory.CreateEmbeddingGenerator(settings));
        return new MovieSearch(collection, embedder);
    }

    public async Task<IReadOnlyList<MovieHit>> SearchAsync(
        string query, MovieFilter? filter = null, int top = 5, CancellationToken ct = default)
    {
        var vector = await embedder.EmbedQueryAsync(query, ct);
        return await SearchAsync(vector, filter, top, ct);
    }

    public async Task<IReadOnlyList<MovieHit>> SearchAsync(
        ReadOnlyMemory<float> vector, MovieFilter? filter = null, int top = 5, CancellationToken ct = default)
    {
        var options = new VectorSearchOptions<MoviePlot> { Filter = ToExpression(filter ?? MovieFilter.None) };
        var hits = new List<MovieHit>(top);
        await foreach (var result in collection.SearchAsync(vector, top, options, ct))
            hits.Add(new MovieHit(result.Record, result.Score ?? 0));
        return hits;
    }

    /// <summary>
    /// Connectors translate the expression tree to SQL, so it must only contain conditions that are
    /// actually set - a "filter.MinYear == null ||" style expression would not translate.
    /// </summary>
    private static Expression<Func<MoviePlot, bool>>? ToExpression(MovieFilter filter)
    {
        var conditions = new List<Expression<Func<MoviePlot, bool>>>();
        if (filter.MinYear is { } min) conditions.Add(m => m.Year >= min);
        if (filter.MaxYear is { } max) conditions.Add(m => m.Year <= max);
        if (!string.IsNullOrWhiteSpace(filter.Genre))
        {
            var genre = Genres.Normalize(filter.Genre);
            conditions.Add(m => m.Genres.Contains(genre));
        }

        if (conditions.Count == 0)
            return null;

        var parameter = Expression.Parameter(typeof(MoviePlot), "m");
        var body = conditions
            .Select(c => new ReplaceParameter(c.Parameters[0], parameter).Visit(c.Body))
            .Aggregate(Expression.AndAlso);
        return Expression.Lambda<Func<MoviePlot, bool>>(body, parameter);
    }

    private sealed class ReplaceParameter(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
