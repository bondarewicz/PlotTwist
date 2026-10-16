namespace PlotTwist.IntegrationTests;

using Infrastructure.Embeddings;
using Polly;
using Polly.Timeout;

// Exercises the exact pipeline PlotEmbedder runs (Infrastructure.Embeddings.EmbeddingResilience), with a short
// timeout and no retry delay so the test doesn't wait on the real 2-minute/1-second production values.
public sealed class EmbeddingResilienceTests
{
    [Fact]
    public async Task Retries_its_own_timeout_and_succeeds_once_the_call_is_fast_again()
    {
        var builder = new ResiliencePipelineBuilder();
        EmbeddingResilience.Configure(
            builder,
            timeout: TimeSpan.FromMilliseconds(20),
            retryDelay: TimeSpan.FromMilliseconds(1)
        );
        var pipeline = builder.Build();
        var attempts = 0;

        var result = await pipeline.ExecuteAsync(
            async ct =>
            {
                attempts++;
                if (attempts < 3)
                    await Task.Delay(TimeSpan.FromSeconds(10), ct);
                return attempts;
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(3, result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task A_timeout_that_never_recovers_still_fails_after_the_retry_budget()
    {
        var builder = new ResiliencePipelineBuilder();
        EmbeddingResilience.Configure(
            builder,
            timeout: TimeSpan.FromMilliseconds(10),
            retryDelay: TimeSpan.FromMilliseconds(1)
        );
        var pipeline = builder.Build();
        var attempts = 0;

        await Assert.ThrowsAsync<TimeoutRejectedException>(() =>
            pipeline
                .ExecuteAsync(
                    async ct =>
                    {
                        attempts++;
                        await Task.Delay(TimeSpan.FromSeconds(10), ct);
                        return attempts;
                    },
                    TestContext.Current.CancellationToken
                )
                .AsTask()
        );

        Assert.Equal(5, attempts); // the initial attempt plus 4 retries
    }
}
