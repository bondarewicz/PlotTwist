namespace PlotTwist.Application.Embedding;

using Abstractions;

public sealed record BuildVectorIndex(bool Rebuild = false);

public sealed class BuildVectorIndexHandler(IPlotEmbedder embedder, IVectorIndex index)
{
    public Task<VectorIndexStatus> Handle(BuildVectorIndex command, CancellationToken ct = default) =>
        index.Build(embedder.Model, command.Rebuild, ct);
}
