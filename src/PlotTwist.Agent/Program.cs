// Step 3: the same search behind a Microsoft Agent Framework agent, wired up two different ways.
//
//   dotnet run --project src/PlotTwist.Agent                 # --mode context (default)
//   dotnet run --project src/PlotTwist.Agent -- --mode tool
//
// context: a TextSearchProvider searches before EVERY model call and injects the hits as context.
//          Predictable, but it also searches for "thanks!", and it can only pass the raw question - no filters.
// tool:    search_movies is a function the model MAY call, and it fills in the year/genre filters itself.
//          Cheaper and smarter, but the model can skip the search and answer from memory instead.
//
// Try the same questions in both modes, e.g.
//   "a 90s sci-fi film where astronauts are stranded"
//   "what's that comedy where a weatherman keeps waking up on the same day?"
//   "something like Jaws but newer than 2000"

using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using PlotTwist.Core;
using PlotTwist.Core.Ollama;
using PlotTwist.Core.Search;

const string Instructions = """
    You are PlotTwist, a film buff who helps people find movies from a database of ~35,000 Wikipedia plot summaries.
    Only recommend films that appear in the search results you were given; never invent titles.
    Cite each film as "Title (Year)" and say in one sentence why it matches. If nothing fits, say so.
    """;

var mode = args is ["--mode", var m] ? m : "context";
var settings = PlotTwistSettings.Load();
var search = MovieSearch.Create(settings);
var chatClient = OllamaFactory.CreateChatClient(settings);

AIAgent agent = mode switch
{
    "context" => chatClient.AsBuilder().BuildAIAgent(new ChatClientAgentOptions
    {
        Name = "PlotTwist",
        ChatOptions = new() { Instructions = Instructions },
        AIContextProviders =
        [
            new TextSearchProvider(SearchForContextAsync, new TextSearchProviderOptions
            {
                SearchTime = TextSearchProviderOptions.TextSearchBehavior.BeforeAIInvoke,
            }),
        ],
    }),

    "tool" => new ChatClientAgent(
        chatClient,
        instructions: Instructions + "\nUse the search_movies tool for every request, and set its filters when the user mentions a decade, year or genre.",
        name: "PlotTwist",
        tools: [AIFunctionFactory.Create(SearchMoviesAsync, "search_movies")]),

    _ => throw new ArgumentException($"Unknown mode '{mode}', use context or tool."),
};

Console.WriteLine($"PlotTwist agent ({mode} mode, {settings.ChatModel}). Describe a film; empty line to quit.");
var session = await agent.CreateSessionAsync();
while (true)
{
    Console.Write("\n> ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input))
        break;

    await foreach (var update in agent.RunStreamingAsync(input, session))
        Console.Write(update.Text);
    Console.WriteLine();
}

// context mode: the provider hands over the latest user message(s) as plain text.
async Task<IEnumerable<TextSearchProvider.TextSearchResult>> SearchForContextAsync(string query, CancellationToken ct)
{
    var hits = await search.SearchAsync(query, top: 8, ct: ct);
    Trace($"[search] \"{Shorten(query)}\" -> {hits.Count} hits");
    return hits.Select(h => new TextSearchProvider.TextSearchResult
    {
        SourceName = $"{h.Movie.Title} ({h.Movie.Year})",
        SourceLink = h.Movie.WikiUrl,
        Text = $"{h.Movie}. Directed by {h.Movie.Director}. {h.Movie.Summary}",
    });
}

// tool mode: the [Description]s become the JSON schema the model sees, so they are part of the prompt.
[Description("Semantic search over film plot summaries. Returns the closest matches with year, genres and summary.")]
async Task<string> SearchMoviesAsync(
    [Description("What happens in the film, in natural language. Leave out years and genres; use the filters for those.")] string plot,
    [Description("Earliest release year, e.g. 1990 for 'the 90s'.")] int? minYear = null,
    [Description("Latest release year, e.g. 1999 for 'the 90s'.")] int? maxYear = null,
    [Description("A single genre such as comedy, drama, horror, thriller, science fiction, animated, western.")] string? genre = null)
{
    var filter = new MovieFilter(minYear, maxYear, genre);
    var hits = await search.SearchAsync(plot, filter, top: 8);
    Trace($"[search_movies] \"{plot}\" where {filter} -> {hits.Count} hits");
    return hits.Count == 0
        ? "No films matched. Try fewer filters."
        : string.Join("\n\n", hits.Select(h => $"{h.Movie} - {h.Movie.Summary}"));
}

static void Trace(string message)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine(message);
    Console.ResetColor();
}

static string Shorten(string s) => s.Length <= 80 ? s.ReplaceLineEndings(" ") : s[..77].ReplaceLineEndings(" ") + "...";
