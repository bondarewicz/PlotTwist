namespace PlotTwist.Application.Verification;

using Abstractions;

public sealed class VerifyIngestionHandler(IPlotEmbedder embedder, IIngestionReport report, IVectorIndex index)
{
    public const double MaxRejectedShare = 0.05;

    public async Task<VerificationReport> Handle(CancellationToken ct = default)
    {
        var model = embedder.Model;
        var status = await report.Get(model, ct);
        var indexStatus = await index.Status(model, ct);
        var checks = new List<VerificationCheck>();

        if (status.LastImport is not { } import)
        {
            checks.Add(new("Import has run", false, "No completed import. Run `import`."));
            return new VerificationReport(model.ToString(), checks);
        }

        var rejectedShare = import.Rejected / (double)Math.Max(import.Read, 1);
        checks.Add(
            new(
                "Import accounted for every row",
                import.Loaded + import.Rejected <= import.Read && import.Read > 0,
                $"read {import.Read:N0} = loaded {import.Loaded:N0} movies + rejected {import.Rejected:N0} rows + rows merged into co-productions ({import.Read - import.Loaded - import.Rejected:N0})"
            )
        );
        checks.Add(
            new(
                "Rejected rows within tolerance",
                rejectedShare <= MaxRejectedShare,
                $"{rejectedShare:P1} rejected (limit {MaxRejectedShare:P0}); see movie_rejects for run {import.RunId}"
            )
        );
        checks.Add(
            new(
                "Movies stored",
                status.Movies >= import.Loaded,
                $"{status.Movies:N0} movies in the database, last import loaded {import.Loaded:N0}"
            )
        );
        checks.Add(
            new(
                "Every movie embedded",
                status.Pending == 0,
                status.Pending == 0
                    ? $"{status.Embedded:N0} embedded in {model}"
                    : $"{status.Pending:N0} pending in {model}. Run `embed`."
            )
        );
        checks.Add(
            new(
                "Vector index exists",
                indexStatus.Exists,
                indexStatus.Exists
                    ? $"{indexStatus.Name}, {indexStatus.Size}"
                    : $"{indexStatus.Name} missing. Run `index`."
            )
        );

        return new VerificationReport(model.ToString(), checks);
    }
}

public sealed record VerificationCheck(string Name, bool Passed, string Detail);

public sealed record VerificationReport(string Model, IReadOnlyList<VerificationCheck> Checks)
{
    public bool Passed => Checks.All(c => c.Passed);
}
