namespace PlotTwist.Application.Import;

// Limit keeps only the first N films, for a quicker demo database.
public sealed record ImportMovies(int? Limit = null);

public sealed record ImportResult(
    long RunId,
    string Source,
    string Revision,
    int Staged,
    int Imported,
    int New,
    int Rejected,
    TimeSpan Elapsed);
