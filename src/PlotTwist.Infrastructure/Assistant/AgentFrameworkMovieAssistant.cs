namespace PlotTwist.Infrastructure.Assistant;

using Application.Search;
using Configuration;
using Microsoft.Extensions.AI;
using PlotTwist.Application.Assistant;

public sealed class AgentFrameworkMovieAssistant(
    IChatClient chatClient,
    SearchMoviesHandler searchMovies,
    ChatModelOptions chat,
    AssistantOptions assistant
) : IMovieAssistant
{
    public string Model => chat.Model;

    public string ProviderName => chat.Provider.ToString();

    public IMovieConversation StartConversation(AssistantMode mode) =>
        new AgentFrameworkMovieConversation(chatClient, searchMovies, chat.Model, assistant, mode);
}
