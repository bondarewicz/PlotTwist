namespace PlotTwist.Application.Abstractions;

// Where films come from, pinned to an exact version: the same snapshot always produces the same database.
public interface IMovieSource
{
    // Downloads the pinned revision if it isn't cached, and verifies its checksum either way.
    Task<SourceSnapshot> Fetch(CancellationToken ct = default);
}

// Provenance of one import, recorded with the run so any database can be traced back to its exact input.
public sealed record SourceSnapshot(string Name, string Revision, string Sha256, string Licence, string LocalPath);
