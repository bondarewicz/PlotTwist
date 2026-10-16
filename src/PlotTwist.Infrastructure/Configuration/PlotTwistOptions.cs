namespace PlotTwist.Infrastructure.Configuration;

using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

public sealed class PlotTwistOptions
{
    public const string Section = "PlotTwist";

    [ValidateObjectMembers]
    public DatabaseOptions Database { get; set; } = new();

    [ValidateObjectMembers]
    public EmbeddingOptions Embeddings { get; set; } = new();

    [ValidateObjectMembers]
    public ChatModelOptions Chat { get; set; } = new();

    [ValidateObjectMembers]
    public AssistantOptions Assistant { get; set; } = new();

    public DatasetOptions Dataset { get; set; } = new();
}

public sealed class DatabaseOptions
{
    [Required]
    public string ConnectionString { get; set; } = "";

    [Range(2, 100)]
    public int HnswM { get; set; } = 16;

    [Range(4, 1000)]
    public int HnswEfConstruction { get; set; } = 64;

    [Required]
    [RegularExpression(@"^\d+(kB|MB|GB)$")]
    public string IndexBuildMemory { get; set; } = "512MB";

    [Range(0, 16)]
    public int IndexBuildWorkers { get; set; } = 2;
}

public enum AiProvider
{
    Ollama,
    OpenAI,
}

public abstract class AiModelOptions
{
    public AiProvider Provider { get; set; } = AiProvider.Ollama;

    [Required]
    public string Model { get; set; } = "";

    public string? Endpoint { get; set; }

    public string? ApiKey { get; set; }
}

public sealed class ChatModelOptions : AiModelOptions;

public sealed class EmbeddingOptions : AiModelOptions
{
    [Range(1, 16000)]
    public int Dimensions { get; set; }

    public string DocumentPrefix { get; set; } = "";

    public string QueryPrefix { get; set; } = "";

    [Range(1, 2048)]
    public int BatchSize { get; set; } = 64;

    [Range(1, 32)]
    public int MaxConcurrency { get; set; } = 1;
}

public sealed class AssistantOptions
{
    [Required]
    public string Name { get; set; } = "";

    [Required]
    public string Instructions { get; set; } = "";

    [Required]
    public string ToolInstructions { get; set; } = "";

    public string? ContextPrompt { get; set; }

    [Range(1, 50)]
    public int ResultsPerSearch { get; set; } = 8;

    [ValidateObjectMembers]
    public SearchToolOptions SearchTool { get; set; } = new();
}

public sealed class SearchToolOptions
{
    [Required]
    public string Name { get; set; } = "";

    [Required]
    public string Description { get; set; } = "";

    [Required]
    public string PlotDescription { get; set; } = "";

    [Required]
    public string ReleasedFromDescription { get; set; } = "";

    [Required]
    public string ReleasedToDescription { get; set; } = "";

    [Required]
    public string GenreDescription { get; set; } = "";
}

public sealed class DatasetOptions
{
    public string Repository { get; set; } = "";

    public string Revision { get; set; } = "";

    public string File { get; set; } = "";

    public string Sha256 { get; set; } = "";

    public string Licence { get; set; } = "";

    public string CacheDirectory { get; set; } = "data";
}

[OptionsValidator]
public sealed partial class PlotTwistOptionsValidator : IValidateOptions<PlotTwistOptions>;
