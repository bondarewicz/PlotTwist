namespace PlotTwist.Application.Assistant;

using Search;

public sealed record AssistantAnswer(
    string Text,
    AssistantMode Mode,
    string ChatModel,
    IReadOnlyList<AssistantSearch> Searches,
    AssistantUsage? Usage,
    TimeSpan Elapsed
);

public sealed record AssistantSearch(string RequestedBy, FindMoviesResult Result);

public sealed record AssistantUsage(long? InputTokens, long? OutputTokens);
