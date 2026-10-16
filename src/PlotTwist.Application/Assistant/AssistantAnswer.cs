namespace PlotTwist.Application.Assistant;

using Search;

// Answer plus everything needed to check it: which searches ran (with which criteria) and what they found.
// If Searches is empty in Tool mode, the model answered from memory and the answer is unverified.
public sealed record AssistantAnswer(
    string Text,
    AssistantMode Mode,
    string ChatModel,
    IReadOnlyList<AssistantSearch> Searches,
    AssistantUsage? Usage,
    TimeSpan Elapsed
);

// Searched = what the search was called with; who decided depends on the mode (provider vs model).
public sealed record AssistantSearch(string RequestedBy, FindMoviesResult Result);

public sealed record AssistantUsage(long? InputTokens, long? OutputTokens);
