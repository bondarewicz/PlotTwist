namespace PlotTwist.IntegrationTests;

using Infrastructure.Configuration;
using Infrastructure.Embeddings;
using Microsoft.Extensions.AI;
using Polly;

public sealed class PlotEmbedderTests
{
    [Fact]
    public async Task A_provider_returning_the_wrong_vector_count_throws_instead_of_misaligning_results()
    {
        var embedder = new PlotEmbedder(
            new FixedCountGenerator(returns: 1),
            new EmbeddingOptions { Model = "fake", Dimensions = 3 },
            ResiliencePipeline.Empty
        );

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            embedder.EmbedSearchableText(
                [TestData.Movie("Alien"), TestData.Movie("Jaws")],
                TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("1 vectors for 2 inputs", error.Message);
    }

    private sealed class FixedCountGenerator(int returns) : IEmbeddingGenerator<string, Embedding<float>>
    {
        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            Task.FromResult(
                new GeneratedEmbeddings<Embedding<float>>(
                    Enumerable.Range(0, returns).Select(_ => new Embedding<float>(new float[3]))
                )
            );

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
