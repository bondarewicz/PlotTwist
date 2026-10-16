using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration.Attributes;

namespace PlotTwist.Core.Movies;

/// <summary>
/// Wikipedia Movie Plots (with summaries), CC BY-SA 4.0. ~35k films, 1901-2017.
/// Downloaded on first use rather than committed: it is ~95 MB.
/// </summary>
public static class MovieDataset
{
    public static async Task<string> EnsureDownloadedAsync(PlotTwistSettings settings, CancellationToken ct = default)
    {
        var path = Path.GetFullPath(settings.DatasetPath);
        if (File.Exists(path))
            return path;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Console.WriteLine($"Downloading dataset to {path} ...");
        using var http = new HttpClient();
        await using var source = await http.GetStreamAsync(settings.DatasetUrl, ct);
        await using var target = File.Create(path + ".part");
        await source.CopyToAsync(target, ct);
        target.Close();
        File.Move(path + ".part", path);
        return path;
    }

    public static IEnumerable<MoviePlot> Read(string path)
    {
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        var id = 0;
        foreach (var row in csv.GetRecords<Row>())
        {
            id++; // row position: stable for the same file, so re-running ingest upserts instead of duplicating
            if (string.IsNullOrWhiteSpace(row.PlotSummary))
                continue;

            yield return new MoviePlot
            {
                Id = id,
                Title = row.Title.Trim(),
                Year = row.Year,
                Genres = Genres.Parse(row.Genre),
                Origin = row.Origin.Trim(),
                Director = row.Director.Trim(),
                Summary = row.PlotSummary.Trim(),
                WikiUrl = row.WikiPage.Trim(),
            };
        }
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
