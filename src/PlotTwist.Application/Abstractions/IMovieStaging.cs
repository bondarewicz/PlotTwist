namespace PlotTwist.Application.Abstractions;

public interface IMovieStaging
{
    Task<int> Stage(SourceSnapshot snapshot, long runId, CancellationToken ct = default);

    IAsyncEnumerable<RawMovieRow> Read(long runId, CancellationToken ct = default);
}

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
