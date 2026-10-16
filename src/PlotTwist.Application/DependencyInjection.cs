namespace PlotTwist.Application;

using Microsoft.Extensions.DependencyInjection;
using PlotTwist.Application.Assistant;
using PlotTwist.Application.Embedding;
using PlotTwist.Application.Import;
using PlotTwist.Application.Search;
using PlotTwist.Application.Verification;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services) =>
        services
            .AddTransient<ImportMoviesHandler>()
            .AddTransient<EmbedMoviesHandler>()
            .AddTransient<BuildVectorIndexHandler>()
            .AddTransient<VerifyIngestionHandler>()
            .AddTransient<FindMoviesHandler>()
            .AddTransient<CompareSearchStrategiesHandler>()
            .AddTransient<AskAssistantHandler>();
}
