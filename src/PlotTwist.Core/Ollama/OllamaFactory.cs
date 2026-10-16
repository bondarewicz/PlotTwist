using Microsoft.Extensions.AI;
using OllamaSharp;

namespace PlotTwist.Core.Ollama;

/// <summary>OllamaSharp implements the Microsoft.Extensions.AI abstractions, so nothing downstream knows it's Ollama.</summary>
public static class OllamaFactory
{
    public static IChatClient CreateChatClient(PlotTwistSettings settings) =>
        new OllamaApiClient(new Uri(settings.OllamaUrl), settings.ChatModel);

    public static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(PlotTwistSettings settings) =>
        new OllamaApiClient(new Uri(settings.OllamaUrl), settings.EmbeddingModel);
}
