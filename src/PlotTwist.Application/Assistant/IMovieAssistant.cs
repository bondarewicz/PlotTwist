namespace PlotTwist.Application.Assistant;

public interface IMovieAssistant
{
    string Model { get; }

    IMovieConversation StartConversation(AssistantMode mode);
}

// One conversation keeps its history across Ask calls.
public interface IMovieConversation
{
    AssistantMode Mode { get; }

    Task<AssistantAnswer> Ask(string question, CancellationToken ct = default);

    // Streams the answer text; Searches are reported through onSearch as they happen.
    IAsyncEnumerable<string> AskStreaming(string question, Action<AssistantSearch>? onSearch = null, CancellationToken ct = default);
}
