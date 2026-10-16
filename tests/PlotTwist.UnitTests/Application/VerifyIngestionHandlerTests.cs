namespace PlotTwist.UnitTests.Application;

using PlotTwist.Application.Abstractions;
using PlotTwist.Application.Verification;
using PlotTwist.Domain.Embeddings;
using PlotTwist.UnitTests.Fakes;

public sealed class VerifyIngestionHandlerTests
{
    private static readonly LastImport Import = new(1, "fake", Read: 1000, Loaded: 960, Rejected: 30, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Passes_when_everything_is_imported_embedded_and_indexed()
    {
        var report = await Verify(new IngestionSnapshot(Import, Movies: 960, Embedded: 960, Pending: 0), indexExists: true);

        Assert.True(report.Passed);
    }

    [Fact]
    public async Task Fails_while_films_are_pending()
    {
        var report = await Verify(new IngestionSnapshot(Import, Movies: 960, Embedded: 900, Pending: 60), indexExists: true);

        Assert.False(Assert.Single(report.Checks, c => c.Name == "Every film embedded").Passed);
    }

    [Fact]
    public async Task Fails_when_too_many_rows_were_rejected()
    {
        var report = await Verify(new IngestionSnapshot(Import with { Loaded = 900, Rejected = 90 }, 900, 900, 0), indexExists: true);

        Assert.False(Assert.Single(report.Checks, c => c.Name == "Rejected rows within tolerance").Passed);
    }

    [Fact]
    public async Task Fails_without_an_import()
    {
        var report = await Verify(new IngestionSnapshot(null, 0, 0, 0), indexExists: false);

        Assert.False(report.Passed);
    }

    private static Task<VerificationReport> Verify(IngestionSnapshot snapshot, bool indexExists) =>
        new VerifyIngestionHandler(new FakePlotEmbedder(), new FakeReport(snapshot), new FakeIndex(indexExists)).Handle(TestContext.Current.CancellationToken);

    private sealed class FakeReport(IngestionSnapshot snapshot) : IIngestionReport
    {
        public Task<IngestionSnapshot> Get(EmbeddingModel model, CancellationToken ct = default) => Task.FromResult(snapshot);
    }

    private sealed class FakeIndex(bool exists) : IVectorIndex
    {
        public Task<VectorIndexStatus> Status(EmbeddingModel model, CancellationToken ct = default) =>
            Task.FromResult(new VectorIndexStatus("ix", exists, exists ? "1 MB" : null));

        public Task<VectorIndexStatus> Build(EmbeddingModel model, bool rebuild, CancellationToken ct = default) => Status(model, ct);
    }
}
