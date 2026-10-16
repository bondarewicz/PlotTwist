namespace PlotTwist.Application.Embedding;

using System.Diagnostics;
using Abstractions;
using Domain.Embeddings;
using Domain.Movies;

public sealed class EmbedMoviesHandler(
    IPlotEmbedder embedder,
    IMovieRepository movies,
    IMovieEmbeddingRepository embeddings,
    IIngestionRunLog runs
)
{
    public async Task<EmbedResult> Handle(
        EmbedMovies command,
        IProgress<EmbedProgress>? progress = null,
        CancellationToken ct = default
    )
    {
        if (command.BatchSize < 1 || command.MaxConcurrency < 1)
            throw new ArgumentOutOfRangeException(nameof(command), "Batch size and concurrency must be positive.");

        var stopwatch = Stopwatch.StartNew();
        var model = embedder.Model;
        var pending = await embeddings.ListPending(model, ct);
        var work = command.Limit is { } limit ? pending.Take(limit).ToList() : pending;

        var runId = await runs.Start("embed", $"{model}: {work.Count} of {pending.Count} pending", ct);
        var embedded = 0;
        try
        {
            await Parallel.ForEachAsync(
                work.Chunk(command.BatchSize),
                new ParallelOptions { MaxDegreeOfParallelism = command.MaxConcurrency, CancellationToken = ct },
                async (ids, token) =>
                {
                    var batch = await movies.Get(ids, token);
                    var vectors = await embedder.EmbedSearchableText(batch, token);
                    await embeddings.Save(
                        batch
                            .Select((m, i) => new MovieEmbedding(m.Id, model, m.SearchableTextHash, vectors[i]))
                            .ToList(),
                        token
                    );

                    var done = Interlocked.Add(ref embedded, batch.Count);
                    progress?.Report(new EmbedProgress(done, work.Count, stopwatch.Elapsed));
                }
            );

            await runs.Complete(runId, new IngestionCounts(Read: work.Count, Embedded: embedded), ct);
            return new EmbedResult(runId, model.Name, pending.Count, embedded, stopwatch.Elapsed);
        }
        catch (Exception e)
        {
            await runs.RecordOutcome(runId, e, new IngestionCounts(Read: work.Count, Embedded: embedded), ct);
            throw;
        }
    }
}
