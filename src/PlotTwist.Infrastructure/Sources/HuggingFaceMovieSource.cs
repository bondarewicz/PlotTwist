namespace PlotTwist.Infrastructure.Sources;

using System.Security.Cryptography;
using Application.Abstractions;
using Configuration;

public sealed class HuggingFaceMovieSource(HttpClient http, DatasetOptions options) : IMovieSource
{
    public async Task<SourceSnapshot> Fetch(CancellationToken ct = default)
    {
        var path = Path.GetFullPath(
            Path.Combine(options.CacheDirectory, options.Repository.Replace('/', '_'), options.Revision, options.File)
        );
        if (!File.Exists(path))
            await Download(path, ct);

        var actual = await Sha256(path, ct);
        if (!actual.Equals(options.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"{options.Repository}@{options.Revision}/{options.File} has SHA-256 {actual}, expected {options.Sha256}. "
                    + $"Delete {path} to re-download, or update Dataset:Sha256 if the revision really changed."
            );

        return new SourceSnapshot(
            $"huggingface:{options.Repository}/{options.File}",
            options.Revision,
            actual,
            options.Licence,
            path
        );
    }

    private async Task Download(string path, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var url = $"https://huggingface.co/datasets/{options.Repository}/resolve/{options.Revision}/{options.File}";
        await using (var source = await http.GetStreamAsync(url, ct))
        await using (var target = File.Create(path + ".part"))
            await source.CopyToAsync(target, ct);
        File.Move(path + ".part", path, overwrite: true);
    }

    private static async Task<string> Sha256(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
    }
}
