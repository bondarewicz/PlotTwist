namespace PlotTwist.Application.Abstractions;

public interface IMovieSource
{
    Task<SourceSnapshot> Fetch(CancellationToken ct = default);
}

public sealed record SourceSnapshot(string Name, string Revision, string Sha256, string Licence, string LocalPath);
