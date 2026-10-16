namespace PlotTwist.Infrastructure.Embeddings;

using Polly;
using Polly.Timeout;

// Shared with tests so the retry/timeout interaction (Polly's own AddTimeout throws TimeoutRejectedException,
// not TimeoutException) is verified against the exact pipeline the app runs, not a copy of it.
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
