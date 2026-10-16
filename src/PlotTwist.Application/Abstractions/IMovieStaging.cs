namespace PlotTwist.Application.Abstractions;

// Raw rows exactly as the source published them, bulk-loaded before any rule is applied, so a surprising
// result can always be checked against what actually came in (it's a table: query it).
public interface IMovieStaging
{
    // Replaces the staged rows with this snapshot's; returns how many were loaded.
    Task<int> Stage(SourceSnapshot snapshot, long runId, CancellationToken ct = default);

    IAsyncEnumerable<RawMovieRow> Read(long runId, CancellationToken ct = default);
}

// One source row, all text, untouched. Plot and Cast aren't used yet but are kept, so using them later is a
// transform change, not a re-download.
public sealed record RawMovieRow(
    int RowNumber,
    string ReleaseYear,
    string Title,
    string Origin,
    string Director,
    string Cast,
    string Genre,
    string WikiPage,
    string Plot,
    string PlotSummary
);
