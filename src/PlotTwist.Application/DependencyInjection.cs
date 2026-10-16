namespace PlotTwist.Application;

using Assistant;
using Embedding;
using Import;
using Microsoft.Extensions.DependencyInjection;
using Search;
using Verification;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services) =>
        services
            .AddTransient<ImportMoviesHandler>()
            .AddTransient<EmbedMoviesHandler>()
            .AddTransient<BuildVectorIndexHandler>()
            .AddTransient<VerifyIngestionHandler>()
            // Stateless, and resolved once into the singleton IMovieAssistant - singleton keeps that an honest
            // dependency instead of a captive one, rather than a transient outliving its registered lifetime.
            .AddSingleton<FindMoviesHandler>()
            .AddTransient<CompareSearchStrategiesHandler>()
            .AddTransient<AskAssistantHandler>();
}
