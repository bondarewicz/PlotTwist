namespace PlotTwist.Application;

using Microsoft.Extensions.DependencyInjection;
using PlotTwist.Application.Assistant;
using PlotTwist.Application.Ingest;
using PlotTwist.Application.Search;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services) =>
        services
            .AddTransient<IngestMoviesHandler>()
            .AddTransient<FindMoviesHandler>()
            .AddTransient<CompareSearchStrategiesHandler>()
            .AddTransient<AskAssistantHandler>();
}
