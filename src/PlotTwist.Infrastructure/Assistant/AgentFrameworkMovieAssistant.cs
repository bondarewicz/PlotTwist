namespace PlotTwist.Infrastructure.Assistant;

using Application.Search;
using Configuration;
using Microsoft.Extensions.AI;
using PlotTwist.Application.Assistant;

// IMovieAssistant on Microsoft Agent Framework. Retrieval always goes through FindMoviesHandler,
// so the agent searches exactly like the Api and the evaluations do.
public sealed class AgentFrameworkMovieAssistant(
    IChatClient chatClient,
    FindMoviesHandler findMovies,
    ChatModelOptions chat,
    AssistantOptions assistant
) : IMovieAssistant
{
    public string Model => chat.Model;

    public string ProviderName => chat.Provider.ToString();

    public IMovieConversation StartConversation(AssistantMode mode) =>
        new AgentFrameworkMovieConversation(chatClient, findMovies, chat.Model, assistant, mode);
}
