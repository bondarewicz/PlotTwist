namespace PlotTwist.Infrastructure.Embeddings;

using Polly;
using Polly.Timeout;

internal static class EmbeddingResilience
{
    public static void Configure(
        ResiliencePipelineBuilder builder,
        TimeSpan? timeout = null,
        TimeSpan? retryDelay = null
    ) =>
        builder
            .AddRetry(
                new()
                {
                    MaxRetryAttempts = 4,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = retryDelay ?? TimeSpan.FromSeconds(1),
                    ShouldHandle = new PredicateBuilder()
                        .Handle<HttpRequestException>()
                        .Handle<TimeoutException>()
                        .Handle<TaskCanceledException>()
                        .Handle<TimeoutRejectedException>(),
                }
            )
            .AddTimeout(timeout ?? TimeSpan.FromMinutes(2));
}
