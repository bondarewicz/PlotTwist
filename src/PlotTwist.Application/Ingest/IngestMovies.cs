namespace PlotTwist.Application.Ingest;

public sealed record IngestMovies(int? Limit = null, string? Origin = null, int BatchSize = 64);

public sealed record IngestProgress(int Embedded, int Total, TimeSpan Elapsed)
{
    public double PerSecond => Embedded / Math.Max(Elapsed.TotalSeconds, 0.001);
}

public sealed record IngestResult(string Dataset, string EmbeddingModel, int Embedded, TimeSpan Elapsed);
