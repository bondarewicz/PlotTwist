namespace PlotTwist.Infrastructure.Configuration;

using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

// Bound from the "PlotTwist" section of appsettings.json, overridable as usual by appsettings.{Environment}.json,
// user secrets and environment variables (PlotTwist__Chat__Model=gpt-4.1-mini).
// Nothing provider-, model- or prompt-specific lives in code: switching any of them is a config change.
// Validated at startup by PlotTwistOptionsValidator, so a bad config fails fast instead of on the first request.
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

    // Only needed by ingest, so not validated at startup; WikipediaMovieDataset checks it when used.
    public DatasetOptions Dataset { get; set; } = new();
}

public sealed class DatabaseOptions
{
    // Keep "Options=-c hnsw.iterative_scan=relaxed_order" (pgvector 0.8+). Without it the HNSW index hands back
    // ~40 candidates (hnsw.ef_search) and the WHERE clause runs afterwards, so selective filters return fewer
    // rows than asked for - or none. "-c hnsw.ef_search=100" (default 40) widens that candidate list: measured with
    // the retrieval evaluation, recall@5 went 57% -> 61%, and 200 added nothing.
    [Required]
    public string ConnectionString { get; set; } = "";

    // One collection per embedding model: vectors from different models (or dimensions) can't be compared.
    [Required]
    public string Collection { get; set; } = "";
}

public enum AiProvider
{
    // Local Ollama; Endpoint defaults to http://localhost:11434.
    Ollama,

    // OpenAI, or anything OpenAI-compatible via Endpoint: Azure OpenAI (https://<resource>.openai.azure.com/openai/v1/),
    // LM Studio, vLLM, Groq, OpenRouter, ...
    OpenAI,
}

public abstract class AiModelOptions
{
    public AiProvider Provider { get; set; } = AiProvider.Ollama;

    [Required]
    public string Model { get; set; } = "";

    public string? Endpoint { get; set; }

    // Keep out of committed files: user secrets or PlotTwist__Chat__ApiKey / PlotTwist__Embeddings__ApiKey.
    public string? ApiKey { get; set; }
}

public sealed class ChatModelOptions : AiModelOptions;

public sealed class EmbeddingOptions : AiModelOptions
{
    // Must match what the model returns; it sizes the vector column, so changing it needs a new Collection.
    [Range(1, 16000)]
    public int Dimensions { get; set; }

    // Some models are trained with task prefixes (nomic-embed-text: "search_document: " / "search_query: ").
    // Leave empty for models that aren't (OpenAI text-embedding-3-*).
    public string DocumentPrefix { get; set; } = "";

    public string QueryPrefix { get; set; } = "";
}

public sealed class AssistantOptions
{
    [Required]
    public string Name { get; set; } = "";

    // System prompt for both modes.
    [Required]
    public string Instructions { get; set; } = "";

    // Appended in Tool mode, to steer when and how the model calls the search tool.
    [Required]
    public string ToolInstructions { get; set; } = "";

    // Context mode: text placed before the injected search results. Empty keeps the framework default.
    public string? ContextPrompt { get; set; }

    [Range(1, 50)]
    public int ResultsPerSearch { get; set; } = 8;

    [ValidateObjectMembers]
    public SearchToolOptions SearchTool { get; set; } = new();
}

// The tool's name and descriptions are what the model reads to decide when and how to call it,
// so they are prompt text and belong with the other prompts.
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
    public string Url { get; set; } = "";

    // Relative paths resolve against the working directory.
    public string Path { get; set; } = "";
}

[OptionsValidator]
public sealed partial class PlotTwistOptionsValidator : IValidateOptions<PlotTwistOptions>;
