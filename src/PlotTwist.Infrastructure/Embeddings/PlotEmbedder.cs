namespace PlotTwist.Infrastructure.Embeddings;

using Microsoft.Extensions.AI;
using PlotTwist.Application.Abstractions;
using PlotTwist.Domain.Movies;
using PlotTwist.Domain.Search;
using PlotTwist.Infrastructure.Configuration;
using DomainEmbedding = PlotTwist.Domain.Embeddings.Embedding;

// Any IEmbeddingGenerator, plus the model-specific bits from config: task prefixes and expected dimensions.
public sealed class PlotEmbedder(IEmbeddingGenerator<string, Embedding<float>> generator, EmbeddingOptions options) : IPlotEmbedder
{
    public string Model => options.Model;

    public async Task<IReadOnlyList<DomainEmbedding>> EmbedMovies(IReadOnlyList<Movie> movies, CancellationToken ct = default)
    {
        var embeddings = await generator.GenerateAsync(movies.Select(m => options.DocumentPrefix + m.SearchableText), cancellationToken: ct);
        return embeddings.Select(e => ToDomain(e.Vector)).ToList();
    }

    public async Task<DomainEmbedding> EmbedDescription(PlotDescription description, CancellationToken ct = default) =>
        ToDomain(await generator.GenerateVectorAsync(options.QueryPrefix + description.Value, cancellationToken: ct));

    // A mismatch would otherwise surface as an obscure pgvector error, or worse, as silently meaningless scores.
    private DomainEmbedding ToDomain(ReadOnlyMemory<float> vector) =>
        vector.Length == options.Dimensions
            ? new DomainEmbedding(vector.Span)
            : throw new InvalidOperationException(
                $"{options.Model} returned {vector.Length} dimensions but {PlotTwistOptions.Section}:Embeddings:Dimensions is {options.Dimensions}. " +
                "Fix Dimensions and use a new Database:Collection, then re-ingest.");
}
