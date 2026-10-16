namespace PlotTwist.Application.Assistant;

public interface IMovieAssistant
{
    string Model { get; }

    string ProviderName { get; }

    IMovieConversation StartConversation(AssistantMode mode);
}

public interface IMovieConversation
{
    AssistantMode Mode { get; }

    Task<AssistantAnswer> Ask(string question, CancellationToken ct = default);

    IAsyncEnumerable<string> AskStreaming(
        string question,
        Action<AssistantSearch>? onSearch = null,
        CancellationToken ct = default
    );
}
