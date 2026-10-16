namespace PlotTwist.Infrastructure.Assistant;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Application.Search;
using Configuration;
using Domain;
using Domain.Search;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using PlotTwist.Application.Assistant;

internal sealed class AgentFrameworkMovieConversation : IMovieConversation
{
    private readonly SearchMoviesHandler searchMovies;
    private readonly string model;
    private readonly AssistantOptions options;
    private readonly AIAgent agent;
    private readonly Lock gate = new();
    private readonly List<AssistantSearch> searches = [];
    private AgentSession? session;
    private Action<AssistantSearch>? onSearch;

    public AgentFrameworkMovieConversation(
        IChatClient chatClient,
        SearchMoviesHandler searchMovies,
        string model,
        AssistantOptions options,
        AssistantMode mode
    )
    {
        this.searchMovies = searchMovies;
        this.model = model;
        this.options = options;
        Mode = mode;
        agent = mode switch
        {
            AssistantMode.Context => CreateContextAgent(chatClient),
            AssistantMode.Tool => CreateToolAgent(chatClient),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
    }

    private AIAgent CreateContextAgent(IChatClient chatClient) =>
        chatClient
            .AsBuilder()
            .BuildAIAgent(
                new ChatClientAgentOptions
                {
                    Name = options.Name,
                    ChatOptions = new() { Instructions = options.Instructions },
                    AIContextProviders =
                    [
                        new TextSearchProvider(
                            SearchForContext,
                            new TextSearchProviderOptions
                            {
                                SearchTime = TextSearchProviderOptions.TextSearchBehavior.BeforeAIInvoke,
                                ContextPrompt = string.IsNullOrWhiteSpace(options.ContextPrompt)
                                    ? null
                                    : options.ContextPrompt,
                            }
                        ),
                    ],
                }
            );

    private AIAgent CreateToolAgent(IChatClient chatClient) =>
        new ChatClientAgent(
            chatClient,
            instructions: options.Instructions + "\n\n" + options.ToolInstructions,
            name: options.Name,
            tools: [new SearchMoviesTool(RunSearchTool, options.SearchTool)]
        );

    public AssistantMode Mode { get; }

    public async Task<AssistantAnswer> Ask(string question, CancellationToken ct = default)
    {
        lock (gate)
        {
            searches.Clear();
            onSearch = null;
        }
        var stopwatch = Stopwatch.StartNew();
        session ??= await agent.CreateSessionAsync(ct);

        var response = await agent.RunAsync(question, session, cancellationToken: ct);

        List<AssistantSearch> ran;
        lock (gate)
            ran = searches.ToList();
        return new AssistantAnswer(
            response.Text,
            Mode,
            model,
            ran,
            response.Usage is { } usage ? new AssistantUsage(usage.InputTokenCount, usage.OutputTokenCount) : null,
            stopwatch.Elapsed
        );
    }

    public async IAsyncEnumerable<string> AskStreaming(
        string question,
        Action<AssistantSearch>? onSearch = null,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        lock (gate)
        {
            searches.Clear();
            this.onSearch = onSearch;
        }
        session ??= await agent.CreateSessionAsync(ct);

        await foreach (var update in agent.RunStreamingAsync(question, session, cancellationToken: ct))
            if (!string.IsNullOrEmpty(update.Text))
                yield return update.Text;
    }

    private async Task<IEnumerable<TextSearchProvider.TextSearchResult>> SearchForContext(
        string query,
        CancellationToken ct
    )
    {
        var text = query.Length <= PlotDescription.MaxLength ? query : query[^PlotDescription.MaxLength..];
        var result = await Search("assistant", PlotDescription.Create(text), MovieCriteria.None, ct);
        return result.Matches.Select(m => new TextSearchProvider.TextSearchResult
        {
            SourceName = m.Movie.ToString(),
            SourceLink = m.Movie.WikipediaUrl?.ToString(),
            Text =
                $"{m.Movie} [{string.Join(", ", m.Movie.Genres)}]. Directed by {m.Movie.Director}. {m.Movie.PlotSummary}",
        });
    }

    private async Task<string> RunSearchTool(
        string plot,
        int? releasedFrom,
        int? releasedTo,
        string? genre,
        CancellationToken ct
    )
    {
        try
        {
            var result = await Search(
                "model",
                PlotDescription.Create(plot),
                MovieCriteria.Create(releasedFrom, releasedTo, genre),
                ct
            );
            return result.Matches.Count == 0
                ? "No movies matched. Try fewer filters."
                : string.Join(
                    "\n\n",
                    result.Matches.Select(m =>
                        $"{m.Movie} [{string.Join(", ", m.Movie.Genres)}] - {m.Movie.PlotSummary}"
                    )
                );
        }
        catch (DomainException e)
        {
            return $"Invalid search: {e.Message}";
        }
    }

    private async Task<SearchMoviesResult> Search(
        string requestedBy,
        PlotDescription plot,
        MovieCriteria criteria,
        CancellationToken ct
    )
    {
        var result = await searchMovies.Handle(new SearchMovies(plot, criteria, options.MatchesPerSearch), ct);
        var search = new AssistantSearch(requestedBy, result);
        Action<AssistantSearch>? callback;
        lock (gate)
        {
            searches.Add(search);
            callback = onSearch;
        }
        callback?.Invoke(search);
        return result;
    }
}
