namespace PlotTwist.Application.Embedding;

public sealed record EmbedMovies(int BatchSize = 64, int MaxConcurrency = 1, int? Limit = null);

public sealed record EmbedProgress(int Embedded, int Total, TimeSpan Elapsed)
{
    public double PerSecond => Embedded / Math.Max(Elapsed.TotalSeconds, 0.001);
}

public sealed record EmbedResult(long RunId, string Model, int Pending, int Embedded, TimeSpan Elapsed);
