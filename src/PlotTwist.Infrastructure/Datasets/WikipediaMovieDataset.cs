namespace PlotTwist.Infrastructure.Datasets;

using System.Globalization;
using System.Runtime.CompilerServices;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using Microsoft.Extensions.Options;
using PlotTwist.Infrastructure.Configuration;
using PlotTwist.Application.Abstractions;
using PlotTwist.Domain;
using PlotTwist.Domain.Movies;

// Wikipedia Movie Plots with summaries, CC BY-SA 4.0: ~35k films, 1901-2017.
// Downloaded on first use rather than committed (~95 MB).
public sealed class WikipediaMovieDataset(HttpClient http, IOptions<PlotTwistOptions> options) : IMovieDataset
{
    public string Name => "wikipedia-movie-plots";

    public async IAsyncEnumerable<Movie> Read([EnumeratorCancellation] CancellationToken ct = default)
    {
        var path = await EnsureDownloaded(ct);
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        var position = 0;
        await foreach (var row in csv.GetRecordsAsync<Row>(ct))
        {
            position++; // row position is the id: stable for the same file
            if (TryMap(position, row) is { } movie)
                yield return movie;
        }
    }

    // Rows that break domain rules (no summary, impossible year) are skipped rather than failing the import.
    private static Movie? TryMap(int position, Row row)
    {
        try
        {
            return new Movie(
                MovieId.Create(position),
                row.Title,
                ReleaseYear.Create(row.Year),
                Genre.ParseList(row.Genre),
                row.Origin,
                row.Director,
                row.PlotSummary,
                Uri.TryCreate(row.WikiPage, UriKind.Absolute, out var url) ? url : null);
        }
        catch (DomainException)
        {
            return null;
        }
    }

    private async Task<string> EnsureDownloaded(CancellationToken ct)
    {
        var dataset = options.Value.Dataset;
        if (string.IsNullOrWhiteSpace(dataset.Url) || string.IsNullOrWhiteSpace(dataset.Path))
            throw new InvalidOperationException($"{PlotTwistOptions.Section}:Dataset:Url and :Path must be configured to ingest.");

        var path = Path.GetFullPath(dataset.Path);
        if (File.Exists(path))
            return path;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using (var source = await http.GetStreamAsync(dataset.Url, ct))
        await using (var target = File.Create(path + ".part"))
            await source.CopyToAsync(target, ct);
        File.Move(path + ".part", path);
        return path;
    }

    private sealed class Row
    {
        [Name("Release Year")] public int Year { get; set; }
        [Name("Title")] public string Title { get; set; } = "";
        [Name("Origin/Ethnicity")] public string Origin { get; set; } = "";
        [Name("Director")] public string Director { get; set; } = "";
        [Name("Genre")] public string Genre { get; set; } = "";
        [Name("Wiki Page")] public string WikiPage { get; set; } = "";
        [Name("PlotSummary")] public string PlotSummary { get; set; } = "";
    }
}
