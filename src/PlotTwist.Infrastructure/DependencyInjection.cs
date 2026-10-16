namespace PlotTwist.Infrastructure;

using Ai;
using Application.Abstractions;
using Application.Search;
using Assistant;
using Configuration;
using Domain.Movies;
using Embeddings;
using FluentMigrator.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Persistence;
using Persistence.Migrations;
using PlotTwist.Application.Assistant;
using PlotTwist.Domain.Embeddings;
using Polly;
using Polly.Registry;
using Sources;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<PlotTwistOptions>()
            .Bind(configuration.GetSection(PlotTwistOptions.Section))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<PlotTwistOptions>, PlotTwistOptionsValidator>();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<PlotTwistOptions>>().Value);
        services.AddSingleton(sp => Options(sp).Database);
        services.AddSingleton(sp => Options(sp).Embeddings);
        services.AddSingleton(sp => Options(sp).Chat);
        services.AddSingleton(sp => Options(sp).Assistant);
        services.AddSingleton(sp => Options(sp).Dataset);

        var connectionString = configuration[$"{PlotTwistOptions.Section}:Database:ConnectionString"];

        services.AddSingleton(_ =>
        {
            var builder = new NpgsqlDataSourceBuilder(connectionString);
            builder.UseVector();
            return builder.Build();
        });
        services.AddSingleton<IMovieRepository, PostgresMovieRepository>();
        services.AddSingleton<PostgresMovieEmbeddingRepository>();
        services.AddSingleton<IMovieEmbeddingRepository>(sp =>
            sp.GetRequiredService<PostgresMovieEmbeddingRepository>()
        );
        services.AddSingleton<IEmbeddingScan>(sp => sp.GetRequiredService<PostgresMovieEmbeddingRepository>());
        services.AddSingleton<IMovieStaging, PostgresMovieStaging>();
        services.AddSingleton<PostgresIngestionRuns>();
        services.AddSingleton<IIngestionRunLog>(sp => sp.GetRequiredService<PostgresIngestionRuns>());
        services.AddSingleton<IIngestionReport>(sp => sp.GetRequiredService<PostgresIngestionRuns>());
        services.AddSingleton<IVectorIndex, PostgresVectorIndex>();

        services
            .AddFluentMigratorCore()
            .ConfigureRunner(runner =>
                runner
                    .AddPostgres()
                    .WithGlobalConnectionString(connectionString)
                    .ScanIn(typeof(CreateIngestionRuns).Assembly)
                    .For.Migrations()
            );
        services.AddScoped<IDatabaseMigrator, PostgresDatabaseMigrator>();
        services.AddHttpClient<IMovieSource, HuggingFaceMovieSource>(http => http.Timeout = TimeSpan.FromMinutes(10));

        services.AddResiliencePipeline(
            PlotEmbedder.ResiliencePipelineName,
            builder => EmbeddingResilience.Configure(builder)
        );
        services.AddSingleton<IPlotEmbedder>(sp =>
        {
            var embeddings = sp.GetRequiredService<EmbeddingOptions>();
            return new PlotEmbedder(
                AiClientFactory.CreateEmbeddingGenerator(embeddings),
                embeddings,
                sp.GetRequiredService<ResiliencePipelineProvider<string>>()
                    .GetPipeline(PlotEmbedder.ResiliencePipelineName)
            );
        });
        services.AddSingleton<IMovieAssistant>(sp =>
        {
            var chat = sp.GetRequiredService<ChatModelOptions>();
            return new AgentFrameworkMovieAssistant(
                AiClientFactory.CreateChatClient(chat),
                sp.GetRequiredService<SearchMoviesHandler>(),
                chat,
                sp.GetRequiredService<AssistantOptions>()
            );
        });

        return services;
    }

    private static PlotTwistOptions Options(IServiceProvider sp) => sp.GetRequiredService<PlotTwistOptions>();
}
