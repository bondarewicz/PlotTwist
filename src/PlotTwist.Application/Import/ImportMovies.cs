namespace PlotTwist.Application.Import;

public sealed record ImportMovies(int? Limit = null);

public sealed record ImportResult(
    long RunId,
    string Source,
    string Revision,
    int Staged,
    int Imported,
    int New,
    int Rejected,
    TimeSpan Elapsed
);
