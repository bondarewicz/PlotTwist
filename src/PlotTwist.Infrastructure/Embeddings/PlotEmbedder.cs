namespace PlotTwist.Infrastructure.Embeddings;

using Microsoft.Extensions.AI;
using Polly;
using PlotTwist.Application.Abstractions;
using PlotTwist.Domain.Movies;
using PlotTwist.Domain.Search;
using PlotTwist.Infrastructure.Configuration;
using DomainEmbedding = PlotTwist.Domain.Embeddings.Embedding;
using EmbeddingModel = PlotTwist.Domain.Embeddings.EmbeddingModel;

// Any IEmbeddingGenerator, plus the model-specific bits from config (task prefixes, expected dimensions) and a
// resilience pipeline: a 35k-film embed job will meet a timeout or a rate limit, and one blip shouldn't fail it.
public sealed class PlotEmbedder(
    IEmbeddingGenerator<string, Embedding<float>> generator,
    EmbeddingOptions options,
    ResiliencePipeline resilience) : IPlotEmbedder
{
    public const string ResiliencePipelineName = "embeddings";

    public EmbeddingModel Model { get; } = EmbeddingModel.Create(options.Model, options.Dimensions);

    public async Task<IReadOnlyList<DomainEmbedding>> EmbedMovies(IReadOnlyList<Movie> movies, CancellationToken ct = default)
    {
        var inputs = movies.Select(m => options.DocumentPrefix + m.SearchableText).ToList();
        var embeddings = await resilience.ExecuteAsync(
            async token => await generator.GenerateAsync(inputs, cancellationToken: token), ct);
        return embeddings.Select(e => ToDomain(e.Vector)).ToList();
    }

    public async Task<DomainEmbedding> EmbedDescription(PlotDescription description, CancellationToken ct = default) =>
        ToDomain(await resilience.ExecuteAsync(
            async token => await generator.GenerateVectorAsync(options.QueryPrefix + description.Value, cancellationToken: token), ct));

    // A mismatch would otherwise surface as an obscure pgvector error, or worse, as silently meaningless scores.
    private DomainEmbedding ToDomain(ReadOnlyMemory<float> vector) =>
        vector.Length == options.Dimensions
            ? new DomainEmbedding(vector.Span)
            : throw new InvalidOperationException(
                $"{options.Model} returned {vector.Length} dimensions but {PlotTwistOptions.Section}:Embeddings:Dimensions is {options.Dimensions}.");
}
