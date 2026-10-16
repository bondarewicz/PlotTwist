namespace PlotTwist.Application.Ingest;

using System.Diagnostics;
using PlotTwist.Application.Abstractions;
using PlotTwist.Domain.Movies;

// Dataset -> embeddings -> store, in batches so a failure part-way keeps what was already saved.
// Re-running is safe: ids come from the dataset, so movies are replaced, not duplicated.
public sealed class IngestMoviesHandler(
    IMovieDataset dataset,
    IPlotEmbedder embedder,
    IMovieRepository movies,
    IStorageInitializer storage)
{
    public async Task<IngestResult> Handle(
        IngestMovies command,
        IProgress<IngestProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (command.BatchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(command), command.BatchSize, "Batch size must be positive.");

        await storage.EnsureCreated(ct);

        var selected = new List<Movie>();
        await foreach (var movie in dataset.Read(ct))
        {
            if (command.Origin is not null && !movie.Origin.Equals(command.Origin, StringComparison.OrdinalIgnoreCase))
                continue;
            selected.Add(movie);
            if (selected.Count == command.Limit)
                break;
        }

        var stopwatch = Stopwatch.StartNew();
        var embedded = 0;
        foreach (var batch in selected.Chunk(command.BatchSize))
        {
            var embeddings = await embedder.EmbedMovies(batch, ct);
            for (var i = 0; i < batch.Length; i++)
                batch[i].AssignPlotEmbedding(embeddings[i]);

            await movies.Save(batch, ct);
            embedded += batch.Length;
            progress?.Report(new IngestProgress(embedded, selected.Count, stopwatch.Elapsed));
        }

        return new IngestResult(dataset.Name, embedder.Model, embedded, stopwatch.Elapsed);
    }
}
