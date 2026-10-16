namespace PlotTwist.Application.Verification;

using PlotTwist.Application.Abstractions;

// Post-load checks: the numbers must add up before the data is trusted. Run after import + embed + index,
// then run the retrieval evaluation before pointing the Api at a new model.
public sealed class VerifyIngestionHandler(IPlotEmbedder embedder, IIngestionReport report, IVectorIndex index)
{
    // Above this share of rejected rows the source (or the policy) changed enough that a human should look.
    public const double MaxRejectedShare = 0.05;

    public async Task<VerificationReport> Handle(CancellationToken ct = default)
    {
        var model = embedder.Model;
        var snapshot = await report.Get(model, ct);
        var indexStatus = await index.Status(model, ct);
        var checks = new List<VerificationCheck>();

        if (snapshot.LastImport is not { } import)
        {
            checks.Add(new("Import has run", false, "No completed import. Run `import`."));
            return new VerificationReport(model.ToString(), checks);
        }

        var rejectedShare = import.Rejected / (double)Math.Max(import.Read, 1);
        checks.Add(new("Import accounted for every row", import.Loaded + import.Rejected <= import.Read && import.Read > 0,
            $"read {import.Read:N0} = loaded {import.Loaded:N0} films + rejected {import.Rejected:N0} rows + rows merged into co-productions ({import.Read - import.Loaded - import.Rejected:N0})"));
        checks.Add(new("Rejected rows within tolerance", rejectedShare <= MaxRejectedShare,
            $"{rejectedShare:P1} rejected (limit {MaxRejectedShare:P0}); see movie_rejects for run {import.RunId}"));
        checks.Add(new("Films stored", snapshot.Movies >= import.Loaded,
            $"{snapshot.Movies:N0} films in the database, last import loaded {import.Loaded:N0}"));
        checks.Add(new("Every film embedded", snapshot.Pending == 0,
            snapshot.Pending == 0 ? $"{snapshot.Embedded:N0} embedded in {model}" : $"{snapshot.Pending:N0} pending in {model}. Run `embed`."));
        checks.Add(new("Vector index exists", indexStatus.Exists,
            indexStatus.Exists ? $"{indexStatus.Name}, {indexStatus.Size}" : $"{indexStatus.Name} missing. Run `index`."));

        return new VerificationReport(model.ToString(), checks);
    }
}

public sealed record VerificationCheck(string Name, bool Passed, string Detail);

public sealed record VerificationReport(string Model, IReadOnlyList<VerificationCheck> Checks)
{
    public bool Passed => Checks.All(c => c.Passed);
}
