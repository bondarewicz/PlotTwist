namespace PlotTwist.Infrastructure.Ai;

using System.ClientModel;
using Configuration;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;

// The only place that knows about concrete providers. Everything else uses the Microsoft.Extensions.AI
// abstractions (IChatClient, IEmbeddingGenerator), which is also what Agent Framework builds on.
public static class AiClientFactory
{
    private static readonly Uri DefaultOllamaEndpoint = new("http://localhost:11434");

    public static IChatClient CreateChatClient(ChatModelOptions options) =>
        Validate(options, "Chat").Provider switch
        {
            AiProvider.Ollama => new OllamaApiClient(OllamaEndpoint(options), options.Model),
            AiProvider.OpenAI => CreateOpenAIClient(options, "Chat").GetChatClient(options.Model).AsIChatClient(),
            _ => throw Unsupported(options, "Chat"),
        };

    public static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(EmbeddingOptions options) =>
        Validate(options, "Embeddings").Provider switch
        {
            AiProvider.Ollama => new OllamaApiClient(OllamaEndpoint(options), options.Model),
            AiProvider.OpenAI => CreateOpenAIClient(options, "Embeddings")
                .GetEmbeddingClient(options.Model)
                .AsIEmbeddingGenerator(options.Dimensions),
            _ => throw Unsupported(options, "Embeddings"),
        };

    private static OpenAIClient CreateOpenAIClient(AiModelOptions options, string section)
    {
        // OpenAI itself needs a key; a local OpenAI-compatible server (LM Studio, vLLM) usually ignores it.
        var apiKey =
            options.ApiKey
            ?? (
                options.Endpoint is null
                    ? throw new InvalidOperationException(
                        $"{PlotTwistOptions.Section}:{section}:ApiKey is required for OpenAI. Set it with user secrets or PlotTwist__{section}__ApiKey."
                    )
                    : "not-needed"
            );

        var clientOptions = new OpenAIClientOptions();
        if (options.Endpoint is not null)
            clientOptions.Endpoint = new Uri(options.Endpoint);
        return new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);
    }

    private static T Validate<T>(T options, string section)
        where T : AiModelOptions =>
        string.IsNullOrWhiteSpace(options.Model)
            ? throw new InvalidOperationException($"{PlotTwistOptions.Section}:{section}:Model is not configured.")
            : options;

    private static Uri OllamaEndpoint(AiModelOptions options) =>
        options.Endpoint is null ? DefaultOllamaEndpoint : new Uri(options.Endpoint);

    private static InvalidOperationException Unsupported(AiModelOptions options, string section) =>
        new(
            $"{PlotTwistOptions.Section}:{section}:Provider '{options.Provider}' is not supported. Use one of: {string.Join(", ", Enum.GetNames<AiProvider>())}."
        );
}
