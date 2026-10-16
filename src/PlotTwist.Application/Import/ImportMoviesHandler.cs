namespace PlotTwist.Application.Import;

using System.Diagnostics;
using Abstractions;
using Domain.Movies;

// Extract (pinned, verified) -> stage raw rows -> apply MovieImportPolicy -> upsert films, record rejects.
// No embedding here: that's derived data with its own job (EmbedMoviesHandler), so imports stay fast,
// deterministic and independent of any model being up.
public sealed class ImportMoviesHandler(
    IMovieSource source,
    IMovieStaging staging,
    IMovieRepository movies,
    IIngestionRuns runs
)
{
    private const int UpsertBatchSize = 1000;

    public async Task<ImportResult> Handle(ImportMovies command, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var snapshot = await source.Fetch(ct);
        var runId = await runs.Start(
            "import",
            $"{snapshot.Name}@{snapshot.Revision} sha256:{snapshot.Sha256} ({snapshot.Licence})",
            ct
        );

        var staged = 0;
        var loaded = 0;
        var rejected = 0;
        try
        {
            staged = await staging.Stage(snapshot, runId, ct);

            var rows = new List<RawMovieRow>(staged);
            await foreach (var row in staging.Read(runId, ct))
                rows.Add(row);

            var decision = MovieImportPolicy.Decide(rows);
            var selected = command.Limit is { } limit ? decision.Movies.Take(limit).ToList() : decision.Movies;
            loaded = selected.Count;
            rejected = decision.Rejects.Count;

            var created = 0;
            foreach (var batch in selected.Chunk(UpsertBatchSize))
                created += await movies.Upsert(batch, ct);
            await runs.SaveRejects(runId, decision.Rejects, ct);

            await runs.Complete(runId, new IngestionCounts(Read: staged, Loaded: loaded, Rejected: rejected), ct);
            return new ImportResult(
                runId,
                snapshot.Name,
                snapshot.Revision,
                staged,
                selected.Count,
                created,
                decision.Rejects.Count,
                stopwatch.Elapsed
            );
        }
        catch (Exception e)
        {
            await runs.RecordOutcome(
                runId,
                e,
                new IngestionCounts(Read: staged, Loaded: loaded, Rejected: rejected),
                ct
            );
            throw;
        }
    }
}
