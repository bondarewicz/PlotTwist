namespace PlotTwist.Application.Search;

using System.Diagnostics;
using System.Globalization;
using System.Numerics.Tensors;
using Abstractions;
using Domain.Embeddings;
using Domain.Movies;
using PlotTwist.Domain.Search;

// The teaching use case: one description answered three ways.
// 1. Brute force - load every vector and compare in memory. This is all "semantic search" is: a dot product.
// 2. Indexed     - the same comparison inside the store, using its vector index (HNSW in pgvector).
// 3. Filtered    - indexed similarity plus hard criteria in the same query (only when criteria are given).
public sealed class CompareSearchStrategiesHandler(IPlotEmbedder embedder, IMovieEmbeddingRepository embeddings)
{
    public async Task<SearchComparison> Handle(CompareSearchStrategies query, CancellationToken ct = default)
    {
        var embedding = await embedder.EmbedDescription(query.Description, ct);
        var strategies = new List<StrategyResult>();

        var stopwatch = Stopwatch.StartNew();
        var all = new List<(Movie Movie, Embedding Vector)>();
        await foreach (var item in embeddings.ListAll(embedder.Model, ct))
            all.Add(item);
        var loaded = stopwatch.Elapsed;

        stopwatch.Restart();
        var bruteForce = all.Select(m => new MovieMatch(
                m.Movie,
                TensorPrimitives.CosineSimilarity(embedding.Values.Span, m.Vector.Values.Span)
            ))
            .OrderByDescending(m => m.Similarity)
            .Take(query.Top)
            .ToList();
        strategies.Add(
            new StrategyResult(
                "brute-force",
                bruteForce,
                loaded + stopwatch.Elapsed,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"TensorPrimitives over {all.Count:N0} vectors: load {loaded.TotalMilliseconds:N0} ms + compare {stopwatch.Elapsed.TotalMilliseconds:N0} ms"
                )
            )
        );

        stopwatch.Restart();
        var indexed = await embeddings.FindSimilar(embedding, embedder.Model, MovieCriteria.None, query.Top, ct);
        strategies.Add(
            new StrategyResult("indexed", indexed, stopwatch.Elapsed, "vector index in the store, no filters")
        );

        if (!query.Criteria.IsEmpty)
        {
            stopwatch.Restart();
            var filtered = await embeddings.FindSimilar(embedding, embedder.Model, query.Criteria, query.Top, ct);
            strategies.Add(
                new StrategyResult("filtered", filtered, stopwatch.Elapsed, $"vector index + WHERE {query.Criteria}")
            );
        }

        return new SearchComparison(query.Description, query.Criteria, strategies);
    }
}
