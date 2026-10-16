namespace PlotTwist.Infrastructure;

using Dapper;
using FluentMigrator.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector.Dapper;
using Polly;
using Polly.Registry;
using PlotTwist.Application.Abstractions;
using PlotTwist.Application.Assistant;
using PlotTwist.Application.Search;
using PlotTwist.Domain.Embeddings;
using PlotTwist.Domain.Movies;
using PlotTwist.Infrastructure.Ai;
using PlotTwist.Infrastructure.Assistant;
using PlotTwist.Infrastructure.Configuration;
using PlotTwist.Infrastructure.Embeddings;
using PlotTwist.Infrastructure.Persistence;
using PlotTwist.Infrastructure.Persistence.Migrations;
using PlotTwist.Infrastructure.Sources;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PlotTwistOptions>()
            .Bind(configuration.GetSection(PlotTwistOptions.Section))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<PlotTwistOptions>, PlotTwistOptionsValidator>();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<PlotTwistOptions>>().Value);
        services.AddSingleton(sp => Options(sp).Database);
        services.AddSingleton(sp => Options(sp).Embeddings);
        services.AddSingleton(sp => Options(sp).Dataset);

        // Postgres: one pooled data source with pgvector's type mapping; snake_case columns map to PascalCase.
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(new VectorTypeHandler());
        services.AddSingleton(sp =>
        {
            var builder = new NpgsqlDataSourceBuilder(Options(sp).Database.ConnectionString);
            builder.UseVector();
            return builder.Build();
        });
        services.AddSingleton<IMovieRepository, PostgresMovieRepository>();
        services.AddSingleton<IMovieEmbeddingRepository, PostgresMovieEmbeddingRepository>();
        services.AddSingleton<IMovieStaging, PostgresMovieStaging>();
        services.AddSingleton<PostgresIngestionRuns>();
        services.AddSingleton<IIngestionRuns>(sp => sp.GetRequiredService<PostgresIngestionRuns>());
        services.AddSingleton<IIngestionReport>(sp => sp.GetRequiredService<PostgresIngestionRuns>());
        services.AddSingleton<IVectorIndex, PostgresVectorIndex>();

        // Schema: FluentMigrator, run explicitly by the Cli's `migrate` (never implicitly at Api startup).
        services.AddFluentMigratorCore()
            .ConfigureRunner(runner => runner
                .AddPostgres()
                .WithGlobalConnectionString(configuration[$"{PlotTwistOptions.Section}:Database:ConnectionString"])
                .ScanIn(typeof(M001_CreateIngestionRuns).Assembly).For.Migrations());
        services.AddScoped<IDatabaseMigrator, PostgresDatabaseMigrator>();

        // Source data: a pinned, checksum-verified Hugging Face file.
        services.AddHttpClient<IMovieSource, HuggingFaceMovieSource>(http => http.Timeout = TimeSpan.FromMinutes(10));

        // Models: provider and model come from configuration; see AiClientFactory. Embedding calls retry transient
        // failures (timeouts, connection drops, 429/5xx surfacing as HttpRequestException) with exponential backoff.
        services.AddResiliencePipeline(PlotEmbedder.ResiliencePipelineName, pipeline => pipeline
            .AddRetry(new() { MaxRetryAttempts = 4, BackoffType = DelayBackoffType.Exponential, UseJitter = true, Delay = TimeSpan.FromSeconds(1),
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>().Handle<TimeoutException>().Handle<TaskCanceledException>() })
            .AddTimeout(TimeSpan.FromMinutes(2)));
        services.AddSingleton<IPlotEmbedder>(sp =>
        {
            var embeddings = Options(sp).Embeddings;
            return new PlotEmbedder(
                AiClientFactory.CreateEmbeddingGenerator(embeddings),
                embeddings,
                sp.GetRequiredService<ResiliencePipelineProvider<string>>().GetPipeline(PlotEmbedder.ResiliencePipelineName));
        });
        services.AddSingleton<IMovieAssistant>(sp =>
        {
            var options = Options(sp);
            return new AgentFrameworkMovieAssistant(
                AiClientFactory.CreateChatClient(options.Chat),
                sp.GetRequiredService<FindMoviesHandler>(),
                options.Chat,
                options.Assistant);
        });

        return services;
    }

    private static PlotTwistOptions Options(IServiceProvider sp) => sp.GetRequiredService<PlotTwistOptions>();
}
