namespace PlotTwist.Application.Abstractions;

using PlotTwist.Domain.Movies;

// The source of films to ingest. Rows that break domain rules (no summary, impossible year) are skipped.
public interface IMovieDataset
{
    string Name { get; }

    IAsyncEnumerable<Movie> Read(CancellationToken ct = default);
}
