using Microsoft.Extensions.AI;
using PlotTwist.Core.Movies;

namespace PlotTwist.Core.Search;

/// <summary>
/// nomic-embed-text is trained with task prefixes: documents and queries are embedded differently.
/// Leaving the prefixes off still "works" but measurably hurts retrieval - try it in the eval.
/// </summary>
public sealed class MovieEmbedder(IEmbeddingGenerator<string, Embedding<float>> generator)
{
    private const string DocumentPrefix = "search_document: ";
    private const string QueryPrefix = "search_query: ";

    public async Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedMoviesAsync(
        IReadOnlyList<MoviePlot> movies, CancellationToken ct = default)
    {
        var embeddings = await generator.GenerateAsync(
            movies.Select(m => DocumentPrefix + m.ToEmbeddingText()), cancellationToken: ct);
        return embeddings.Select(e => e.Vector).ToList();
    }

    public async Task<ReadOnlyMemory<float>> EmbedQueryAsync(string query, CancellationToken ct = default) =>
        await generator.GenerateVectorAsync(QueryPrefix + query, cancellationToken: ct);
}
