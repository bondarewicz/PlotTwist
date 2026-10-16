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

// One agent + session per conversation, so the search callbacks can record what this conversation searched.
internal sealed class AgentFrameworkMovieConversation : IMovieConversation
{
    private readonly FindMoviesHandler findMovies;
    private readonly string model;
    private readonly AssistantOptions options;
    private readonly AIAgent agent;
    private readonly List<AssistantSearch> searches = [];
    private AgentSession? session;
    private Action<AssistantSearch>? onSearch;

    public AgentFrameworkMovieConversation(
        IChatClient chatClient,
        FindMoviesHandler findMovies,
        string model,
        AssistantOptions options,
        AssistantMode mode
    )
    {
        this.findMovies = findMovies;
        this.model = model;
        this.options = options;
        Mode = mode;
        agent = mode switch
        {
            // Context: a TextSearchProvider searches with the user's raw message before every model call.
            AssistantMode.Context => chatClient
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
                ),

            // Tool: the model decides whether to search and fills in the criteria itself.
            AssistantMode.Tool => new ChatClientAgent(
                chatClient,
                instructions: options.Instructions + "\n\n" + options.ToolInstructions,
                name: options.Name,
                tools: [new SearchMoviesFunction(SearchMovies, options.SearchTool)]
            ),

            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
    }

    public AssistantMode Mode { get; }

    public async Task<AssistantAnswer> Ask(string question, CancellationToken ct = default)
    {
        searches.Clear();
        onSearch = null;
        var stopwatch = Stopwatch.StartNew();
        session ??= await agent.CreateSessionAsync(ct);

        var response = await agent.RunAsync(question, session, cancellationToken: ct);

        return new AssistantAnswer(
            response.Text,
            Mode,
            model,
            searches.ToList(),
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
        searches.Clear();
        this.onSearch = onSearch;
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
        // The provider hands over recent user messages verbatim; trim to what a description may hold.
        var text = query.Length <= PlotDescription.MaxLength ? query : query[^PlotDescription.MaxLength..];
        var result = await Search("context-provider", PlotDescription.Create(text), MovieCriteria.None, ct);
        return result.Matches.Select(m => new TextSearchProvider.TextSearchResult
        {
            SourceName = m.Movie.ToString(),
            SourceLink = m.Movie.WikipediaUrl?.ToString(),
            Text =
                $"{m.Movie} [{string.Join(", ", m.Movie.Genres)}]. Directed by {m.Movie.Director}. {m.Movie.PlotSummary}",
        });
    }

    private async Task<string> SearchMovies(
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
                ? "No films matched. Try fewer filters."
                : string.Join(
                    "\n\n",
                    result.Matches.Select(m =>
                        $"{m.Movie} [{string.Join(", ", m.Movie.Genres)}] - {m.Movie.PlotSummary}"
                    )
                );
        }
        catch (DomainException e)
        {
            // Bad arguments from the model go back to the model so it can retry, instead of failing the request.
            return $"Invalid search: {e.Message}";
        }
    }

    private async Task<FindMoviesResult> Search(
        string requestedBy,
        PlotDescription description,
        MovieCriteria criteria,
        CancellationToken ct
    )
    {
        var result = await findMovies.Handle(new FindMovies(description, criteria, options.ResultsPerSearch), ct);
        var search = new AssistantSearch(requestedBy, result);
        searches.Add(search);
        onSearch?.Invoke(search);
        return result;
    }
}
