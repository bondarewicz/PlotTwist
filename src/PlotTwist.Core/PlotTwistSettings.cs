using Microsoft.Extensions.Configuration;

namespace PlotTwist.Core;

/// <summary>
/// Everything that points at local infrastructure. Defaults match docker-compose.yml and a stock Ollama;
/// override any of them with environment variables prefixed PLOTTWIST_ (e.g. PLOTTWIST_ChatModel=gemma4).
/// </summary>
public sealed class PlotTwistSettings
{
    public string PostgresConnectionString { get; set; } =
        "Host=localhost;Port=5434;Database=plottwist;Username=postgres;Password=postgres";

    public string OllamaUrl { get; set; } = "http://localhost:11434";

    /// <summary>768-dimensional; must match <see cref="Movies.MoviePlot.Dimensions"/>.</summary>
    public string EmbeddingModel { get; set; } = "nomic-embed-text";

    /// <summary>Needs tool-calling support for the agent's tool mode.</summary>
    public string ChatModel { get; set; } = "qwen3.6";

    public string Collection { get; set; } = "movies";

    public string DatasetUrl { get; set; } =
        "https://huggingface.co/datasets/vishnupriyavr/wiki-movie-plots-with-summaries/resolve/main/wiki_movie_plots_deduped_with_summaries.csv";

    public string DatasetPath { get; set; } = "data/wiki_movie_plots.csv";

    public static PlotTwistSettings Load() =>
        new ConfigurationBuilder()
            .AddEnvironmentVariables("PLOTTWIST_")
            .Build()
            .Get<PlotTwistSettings>() ?? new PlotTwistSettings();
}
