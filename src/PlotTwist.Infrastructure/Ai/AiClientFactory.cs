namespace PlotTwist.Infrastructure.Ai;

using System.ClientModel;
using Configuration;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;

public static class AiClientFactory
{
    private static readonly Uri DefaultOllamaEndpoint = new("http://localhost:11434");

    public static IChatClient CreateChatClient(ChatModelOptions options) =>
        Validate(options, ChatModelOptions.Section).Provider switch
        {
            AiProvider.Ollama => new OllamaApiClient(OllamaEndpoint(options), options.Model),
            AiProvider.OpenAI => CreateOpenAIClient(options, ChatModelOptions.Section)
                .GetChatClient(options.Model)
                .AsIChatClient(),
            _ => throw Unsupported(options, ChatModelOptions.Section),
        };

    public static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(EmbeddingOptions options) =>
        Validate(options, EmbeddingOptions.Section).Provider switch
        {
            AiProvider.Ollama => new OllamaApiClient(OllamaEndpoint(options), options.Model),
            AiProvider.OpenAI => CreateOpenAIClient(options, EmbeddingOptions.Section)
                .GetEmbeddingClient(options.Model)
                .AsIEmbeddingGenerator(options.Dimensions),
            _ => throw Unsupported(options, EmbeddingOptions.Section),
        };

    private static OpenAIClient CreateOpenAIClient(AiModelOptions options, string section)
    {
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
