namespace PlotTwist.Infrastructure.Embeddings;

using Application.Abstractions;
using Configuration;
using Domain.Movies;
using Domain.Search;
using Microsoft.Extensions.AI;
using Polly;
using DomainEmbedding = PlotTwist.Domain.Embeddings.Embedding;
using EmbeddingModel = PlotTwist.Domain.Embeddings.EmbeddingModel;

public sealed class PlotEmbedder(
    IEmbeddingGenerator<string, Embedding<float>> generator,
    EmbeddingOptions options,
    ResiliencePipeline resilience
) : IPlotEmbedder
{
    public const string ResiliencePipelineName = "embeddings";

    public EmbeddingModel Model { get; } = EmbeddingModel.Create(options.Model, options.Dimensions);

    public string ProviderName => options.Provider.ToString();

    public async Task<IReadOnlyList<DomainEmbedding>> EmbedSearchableText(
        IReadOnlyList<Movie> movies,
        CancellationToken ct = default
    )
    {
        var inputs = movies.Select(m => options.DocumentPrefix + m.SearchableText).ToList();
        var embeddings = await resilience.ExecuteAsync(
            async token => await generator.GenerateAsync(inputs, cancellationToken: token),
            ct
        );
        if (embeddings.Count != movies.Count)
            throw new InvalidOperationException(
                $"{options.Model} returned {embeddings.Count} vectors for {movies.Count} inputs."
            );
        return embeddings.Select(e => ToDomain(e.Vector)).ToList();
    }

    public async Task<DomainEmbedding> EmbedPlotDescription(PlotDescription plot, CancellationToken ct = default) =>
        ToDomain(
            await resilience.ExecuteAsync(
                async token =>
                    await generator.GenerateVectorAsync(options.QueryPrefix + plot.Value, cancellationToken: token),
                ct
            )
        );

    private DomainEmbedding ToDomain(ReadOnlyMemory<float> vector) =>
        vector.Length == options.Dimensions
            ? new DomainEmbedding(vector.Span)
            : throw new InvalidOperationException(
                $"{options.Model} returned {vector.Length} dimensions but {PlotTwistOptions.Section}:{EmbeddingOptions.Section}:Dimensions is {options.Dimensions}."
            );
}
