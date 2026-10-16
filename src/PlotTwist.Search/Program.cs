// Steps 1 and 2 side by side: the same question answered three ways.
//
//   dotnet run --project src/PlotTwist.Search -- "a man relives the same day over and over"
//   dotnet run --project src/PlotTwist.Search -- "astronauts stranded in space" --min-year 1990 --max-year 1999 --genre "sci-fi"
//
// 1. Brute force in memory: every vector loaded into a list and compared with TensorPrimitives. This is all
//    "semantic search" is - a dot product against everything.
// 2. pgvector: the same comparison done by Postgres, using the HNSW index instead of scanning every row.
// 3. pgvector + filter: vector similarity AND a SQL WHERE clause in one query (only when filters are given).

using System.Diagnostics;
using System.Numerics.Tensors;
using Microsoft.Extensions.VectorData;
using PlotTwist.Core;
using PlotTwist.Core.Movies;
using PlotTwist.Core.Search;

var query = "";
int? minYear = null, maxYear = null;
string? genre = null;
var top = 5;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--min-year": minYear = int.Parse(args[++i]); break;
        case "--max-year": maxYear = int.Parse(args[++i]); break;
        case "--genre": genre = args[++i]; break;
        case "--top": top = int.Parse(args[++i]); break;
        default: query = args[i]; break;
    }
}

if (query.Length == 0)
{
    Console.WriteLine("""Usage: PlotTwist.Search "<what the film is about>" [--min-year N] [--max-year N] [--genre G] [--top N]""");
    return;
}

var search = MovieSearch.Create(PlotTwistSettings.Load());
var filter = new MovieFilter(minYear, maxYear, genre);

var stopwatch = Stopwatch.StartNew();
var queryVector = await search.Embedder.EmbedQueryAsync(query);
Console.WriteLine($"Query: \"{query}\"  ({queryVector.Length} dims, embedded in {stopwatch.ElapsedMilliseconds} ms)");

// 1. Brute force -------------------------------------------------------------------------------------------
stopwatch.Restart();
var all = new List<MoviePlot>();
await foreach (var movie in search.Collection.GetAsync(_ => true, int.MaxValue, new() { IncludeVectors = true }))
    all.Add(movie);
var loadMs = stopwatch.ElapsedMilliseconds;

stopwatch.Restart();
var bruteForce = all
    .Select(m => new MovieHit(m, TensorPrimitives.CosineSimilarity(queryVector.Span, m.Embedding.Span)))
    .OrderByDescending(h => h.Score)
    .Take(top)
    .ToList();
Print($"1. Brute force, TensorPrimitives over {all.Count:N0} vectors in memory",
    $"load {loadMs} ms (once) + compare {stopwatch.ElapsedMilliseconds} ms", bruteForce);

// 2. pgvector ----------------------------------------------------------------------------------------------
stopwatch.Restart();
var pgvector = await search.SearchAsync(queryVector, top: top);
Print("2. pgvector, HNSW index", $"{stopwatch.ElapsedMilliseconds} ms", pgvector);

// 3. pgvector + filter -------------------------------------------------------------------------------------
if (!filter.IsEmpty)
{
    stopwatch.Restart();
    var filtered = await search.SearchAsync(queryVector, filter, top);
    Print($"3. pgvector + WHERE {filter}", $"{stopwatch.ElapsedMilliseconds} ms", filtered);
}

static void Print(string title, string timing, IReadOnlyList<MovieHit> hits)
{
    Console.WriteLine();
    Console.WriteLine($"{title}  [{timing}]");
    if (hits.Count == 0)
        Console.WriteLine("   (no matches)");
    for (var i = 0; i < hits.Count; i++)
        Console.WriteLine($"   {i + 1}. {hits[i].Score:F3}  {hits[i].Movie}");
}
