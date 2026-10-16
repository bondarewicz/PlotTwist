namespace PlotTwist.IntegrationTests;

using System.Security.Cryptography;
using Application.Abstractions;
using Application.Import;
using Infrastructure.Configuration;
using Infrastructure.Persistence;
using Infrastructure.Sources;

[Collection(PostgresCollection.Name)]
public sealed class IngestionTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string Header = "Release Year,Title,Origin/Ethnicity,Director,Cast,Genre,Wiki Page,Plot,PlotSummary";

    private readonly string directory = Directory.CreateTempSubdirectory("plottwist-").FullName;
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await postgres.Reset();

    public ValueTask DisposeAsync()
    {
        Directory.Delete(directory, recursive: true);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Import_stages_raw_rows_loads_films_and_records_rejects_and_the_run()
    {
        var snapshot = await Csv(
            "1979,Alien,American,Ridley Scott,Sigourney Weaver,sci-fi,https://en.wikipedia.org/wiki/Alien_(film),Long plot.,\"A crew meets a creature, and it's bad.\"",
            "1939,The Outsider,American,Paul Stein,,drama,https://en.wikipedia.org/wiki/The_Outsider_(1939_film),,A summary.",
            "1939,The Outsider,British,Paul Stein,,drama,https://en.wikipedia.org/wiki/The_Outsider_(1939_film),,A summary.",
            "1916,Door,American,,,drama,https://en.wikipedia.org/wiki/Door,,A summary.",
            "2016,Door,American,,,horror,https://en.wikipedia.org/wiki/Door,,A summary."
        );
        var runs = new PostgresIngestionRuns(postgres.DataSource);
        var handler = new ImportMoviesHandler(
            new FixedSource(snapshot),
            new PostgresMovieStaging(postgres.DataSource),
            new PostgresMovieRepository(postgres.DataSource),
            runs
        );

        var result = await handler.Handle(new ImportMovies(), Ct);

        Assert.Equal((5, 2, 2), (result.Staged, result.Imported, result.Rejected));
        var report = await runs.Get(TestData.Small, Ct);
        Assert.Equal((5, 2, 2), (report.LastImport!.Read, report.LastImport.Loaded, report.LastImport.Rejected));
        Assert.Equal((2, 2), (report.Movies, report.Pending));

        var staged = await new PostgresMovieStaging(postgres.DataSource).Read(result.RunId, Ct).ToListAsync(Ct);
        Assert.Equal("Sigourney Weaver", staged[0].Cast); // raw columns kept even though unused today
        Assert.Equal("A crew meets a creature, and it's bad.", staged[0].PlotSummary);
    }

    [Fact]
    public async Task Staging_refuses_a_file_without_the_expected_columns()
    {
        var path = Path.Combine(directory, "bad.csv");
        await File.WriteAllTextAsync(path, "Title,Year\nAlien,1979\n", Ct);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PostgresMovieStaging(postgres.DataSource).Stage(
                new SourceSnapshot("bad", "rev", "sha", "", path),
                1,
                Ct
            )
        );

        Assert.Contains("missing columns", error.Message);
    }

    [Fact]
    public async Task A_cached_file_that_doesnt_match_the_pinned_checksum_stops_the_import()
    {
        var options = new DatasetOptions
        {
            Repository = "org/data",
            Revision = "abc123",
            File = "movies.csv",
            Sha256 = new string('0', 64),
            CacheDirectory = directory,
        };
        var cached = Path.Combine(directory, "org_data", "abc123", "movies.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(cached)!);
        await File.WriteAllTextAsync(cached, "tampered", Ct);

        using var httpClient = new HttpClient();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new HuggingFaceMovieSource(httpClient, options).Fetch(Ct)
        );

        Assert.Contains("expected 0000", error.Message);
    }

    private async Task<SourceSnapshot> Csv(params string[] rows)
    {
        var path = Path.Combine(directory, "movies.csv");
        await File.WriteAllLinesAsync(path, [Header, .. rows], Ct);
        await using var stream = File.OpenRead(path);
        return new SourceSnapshot(
            "test",
            "rev",
            Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, Ct)),
            "CC0",
            path
        );
    }

    private sealed class FixedSource(SourceSnapshot snapshot) : IMovieSource
    {
        public Task<SourceSnapshot> Fetch(CancellationToken ct = default) => Task.FromResult(snapshot);
    }
}
