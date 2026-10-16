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
            .AddSingleton<FindMoviesHandler>()
            .AddTransient<CompareSearchStrategiesHandler>()
            .AddTransient<AskAssistantHandler>();
}
