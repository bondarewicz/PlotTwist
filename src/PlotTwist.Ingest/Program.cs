// Step 0: CSV -> embeddings (Ollama) -> pgvector.
//
//   dotnet run --project src/PlotTwist.Ingest                      # all ~35k films
//   dotnet run --project src/PlotTwist.Ingest -- --limit 5000      # quicker demo set
//   dotnet run --project src/PlotTwist.Ingest -- --origin American # one country only
//
// Re-running is safe: ids are the CSV row numbers, so records are upserted, not duplicated.

using System.Diagnostics;
using PlotTwist.Core;
using PlotTwist.Core.Movies;
using PlotTwist.Core.Search;

const int BatchSize = 64;

var limit = int.MaxValue;
string? origin = null;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--limit": limit = int.Parse(args[++i]); break;
        case "--origin": origin = args[++i]; break;
        default: throw new ArgumentException($"Unknown argument: {args[i]}");
    }
}

var settings = PlotTwistSettings.Load();
var search = MovieSearch.Create(settings);

var path = await MovieDataset.EnsureDownloadedAsync(settings);
var movies = MovieDataset.Read(path)
    .Where(m => origin is null || m.Origin.Equals(origin, StringComparison.OrdinalIgnoreCase))
    .Take(limit)
    .ToList();

// Creates the table and the HNSW index if they don't exist yet (CREATE EXTENSION vector included).
await search.Collection.EnsureCollectionExistsAsync();

Console.WriteLine($"Embedding {movies.Count:N0} films with {settings.EmbeddingModel} into '{settings.Collection}' ...");
var stopwatch = Stopwatch.StartNew();
var done = 0;

foreach (var batch in movies.Chunk(BatchSize))
{
    var vectors = await search.Embedder.EmbedMoviesAsync(batch);
    for (var i = 0; i < batch.Length; i++)
        batch[i].Embedding = vectors[i];

    await search.Collection.UpsertAsync(batch);

    done += batch.Length;
    var rate = done / stopwatch.Elapsed.TotalSeconds;
    Console.Write($"\r  {done,6:N0} / {movies.Count:N0}  ({rate:N0}/s, ~{(movies.Count - done) / rate:N0}s left)   ");
}

Console.WriteLine($"\nDone in {stopwatch.Elapsed:mm\\:ss}.");
