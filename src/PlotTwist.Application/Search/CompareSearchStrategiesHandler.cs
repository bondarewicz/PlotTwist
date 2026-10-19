namespace PlotTwist.Application.Search;

using System.Diagnostics;
using System.Globalization;
using System.Numerics.Tensors;
using Abstractions;
using Domain.Embeddings;
using Domain.Movies;
using PlotTwist.Domain.Search;

public sealed class CompareSearchStrategiesHandler(
    IPlotEmbedder embedder,
    IMovieEmbeddingRepository embeddings,
    IEmbeddingScan scan
)
{
    public async Task<SearchComparison> Handle(CompareSearchStrategies query, CancellationToken ct = default)
    {
        SearchMovies.EnsureValidTop(query.Top);

        var embedding = await embedder.EmbedPlotDescription(query.Plot, ct);

        var strategies = new List<StrategyResult> { await BruteForce(embedding, query.Top, ct) };
        strategies.Add(
            await Search(
                "indexed",
                MovieCriteria.None,
                "vector index in the store, no filters",
                embedding,
                query.Top,
                ct
            )
        );
        if (!query.Criteria.IsEmpty)
            strategies.Add(
                await Search(
                    "filtered",
                    query.Criteria,
                    $"vector index + WHERE {query.Criteria}",
                    embedding,
                    query.Top,
                    ct
                )
            );

        return new SearchComparison(query.Plot, query.Criteria, strategies);
    }

    // shortcut: loads every vector (~100 MB) per call; drop or page it before the Api goes public.
    private async Task<StrategyResult> BruteForce(Embedding embedding, int top, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var all = new List<EmbeddedMovie>();
        await foreach (var item in scan.Scan(embedder.Model, ct))
            all.Add(item);
        var loaded = stopwatch.Elapsed;

        stopwatch.Restart();
        var matches = all.Select(m => new MovieMatch(
                m.Movie,
                TensorPrimitives.CosineSimilarity(embedding.Values.Span, m.Vector.Values.Span)
            ))
            .OrderByDescending(m => m.Similarity)
            .Take(top)
            .ToList();

        return new StrategyResult(
            "brute-force",
            matches,
            loaded + stopwatch.Elapsed,
            string.Create(
                CultureInfo.InvariantCulture,
                $"TensorPrimitives over {all.Count:N0} vectors: load {loaded.TotalMilliseconds:N0} ms + compare {stopwatch.Elapsed.TotalMilliseconds:N0} ms"
            )
        );
    }

    private async Task<StrategyResult> Search(
        string strategy,
        MovieCriteria criteria,
        string notes,
        Embedding embedding,
        int top,
        CancellationToken ct
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var matches = await embeddings.FindSimilar(embedding, embedder.Model, criteria, top, ct);
        return new StrategyResult(strategy, matches, stopwatch.Elapsed, notes);
    }
}
