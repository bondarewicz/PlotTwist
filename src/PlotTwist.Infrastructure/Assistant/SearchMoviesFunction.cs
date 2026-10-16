namespace PlotTwist.Infrastructure.Assistant;

using System.Text.Json;
using System.Text.Json.Nodes;
using Configuration;
using Microsoft.Extensions.AI;

// The search tool as the model sees it. The C# signature fixes the parameters; the name and every description
// come from configuration, because they are prompt text: they decide when and how the model calls the tool.
internal sealed class SearchMoviesFunction : DelegatingAIFunction
{
    public delegate Task<string> SearchMovies(
        string plot,
        int? releasedFrom,
        int? releasedTo,
        string? genre,
        CancellationToken ct
    );

    private readonly SearchToolOptions options;
    private readonly JsonElement schema;

    public SearchMoviesFunction(SearchMovies search, SearchToolOptions options)
        // Bound through this lambda so the parameter names in the schema are fixed here, not taken from whatever
        // method the caller passes (AIFunctionFactory reads names from the target method, not the delegate type).
        : base(
            AIFunctionFactory.Create(
                (
                    string plot,
                    int? releasedFrom = null,
                    int? releasedTo = null,
                    string? genre = null,
                    CancellationToken ct = default
                ) => search(plot, releasedFrom, releasedTo, genre, ct)
            )
        )
    {
        this.options = options;
        schema = DescribeParameters(
            InnerFunction.JsonSchema,
            new()
            {
                ["plot"] = options.PlotDescription,
                ["releasedFrom"] = options.ReleasedFromDescription,
                ["releasedTo"] = options.ReleasedToDescription,
                ["genre"] = options.GenreDescription,
            }
        );
    }

    public override string Name => options.Name;

    public override string Description => options.Description;

    public override JsonElement JsonSchema => schema;

    // Models sometimes send arguments that don't bind ("releasedFrom": "1990s"). Agent Framework rethrows binding
    // errors, which would fail the whole request (a 500 from /movies/ask). Returning the error as the tool result
    // lets the model correct itself instead, like SearchMovies does for broken domain rules.
    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await base.InvokeCoreAsync(arguments, cancellationToken);
        }
        catch (Exception e) when (e is JsonException or ArgumentException)
        {
            return $"Invalid arguments for {Name}: {e.Message} Years must be whole numbers such as 1990.";
        }
    }

    private static JsonElement DescribeParameters(JsonElement original, Dictionary<string, string> descriptions)
    {
        var node = JsonNode.Parse(original.GetRawText())!;
        var properties =
            node["properties"]?.AsObject() ?? throw new InvalidOperationException("Tool schema has no properties.");
        foreach (var (name, description) in descriptions)
        {
            if (properties[name] is not JsonObject property)
                throw new InvalidOperationException($"Tool schema has no parameter '{name}'.");
            if (!string.IsNullOrWhiteSpace(description))
                property["description"] = description;
        }

        return JsonSerializer.SerializeToElement(node);
    }
}
