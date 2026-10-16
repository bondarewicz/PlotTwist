namespace PlotTwist.Infrastructure;

using CommunityToolkit.VectorData.PgVector;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Npgsql;
using PlotTwist.Application.Abstractions;
using PlotTwist.Application.Assistant;
using PlotTwist.Application.Search;
using PlotTwist.Domain.Movies;
using PlotTwist.Infrastructure.Ai;
using PlotTwist.Infrastructure.Assistant;
using PlotTwist.Infrastructure.Configuration;
using PlotTwist.Infrastructure.Datasets;
using PlotTwist.Infrastructure.Embeddings;
using PlotTwist.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PlotTwistOptions>()
            .Bind(configuration.GetSection(PlotTwistOptions.Section))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<PlotTwistOptions>, PlotTwistOptionsValidator>();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<PlotTwistOptions>>().Value);

        // Postgres + pgvector
        services.AddSingleton(sp =>
        {
            var builder = new NpgsqlDataSourceBuilder(Options(sp).Database.ConnectionString);
            builder.UseVector();
            return builder.Build();
        });
        services.AddSingleton(sp => new PostgresVectorStore(sp.GetRequiredService<NpgsqlDataSource>(), ownsDataSource: false));
        services.AddSingleton<VectorStoreCollection<int, MovieRecord>>(sp => sp.GetRequiredService<PostgresVectorStore>().GetCollection<int, MovieRecord>(
            Options(sp).Database.Collection,
            MovieRecordDefinition.Create(Options(sp).Embeddings.Dimensions)));
        services.AddSingleton<PgVectorMovieRepository>();
        services.AddSingleton<IMovieRepository>(sp => sp.GetRequiredService<PgVectorMovieRepository>());
        services.AddSingleton<IStorageInitializer>(sp => sp.GetRequiredService<PgVectorMovieRepository>());

        // Models: provider and model come from configuration; see AiClientFactory.
        services.AddSingleton<IPlotEmbedder>(sp =>
        {
            var embeddings = Options(sp).Embeddings;
            return new PlotEmbedder(AiClientFactory.CreateEmbeddingGenerator(embeddings), embeddings);
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

        services.AddHttpClient<IMovieDataset, WikipediaMovieDataset>();

        return services;
    }

    private static PlotTwistOptions Options(IServiceProvider sp) => sp.GetRequiredService<PlotTwistOptions>();
}
