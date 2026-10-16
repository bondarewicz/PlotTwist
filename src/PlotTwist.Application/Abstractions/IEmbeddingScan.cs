namespace PlotTwist.Application.Abstractions;

using Domain.Embeddings;
using Domain.Movies;

public interface IEmbeddingScan
{
    IAsyncEnumerable<EmbeddedMovie> Scan(EmbeddingModel model, CancellationToken ct = default);
}

public sealed record EmbeddedMovie(Movie Movie, Embedding Vector);
