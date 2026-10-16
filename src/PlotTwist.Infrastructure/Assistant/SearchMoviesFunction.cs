namespace PlotTwist.Infrastructure.Assistant;

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using PlotTwist.Infrastructure.Configuration;

// The search tool as the model sees it. The C# signature fixes the parameters; the name and every description
// come from configuration, because they are prompt text: they decide when and how the model calls the tool.
internal sealed class SearchMoviesFunction : DelegatingAIFunction
{
    public delegate Task<string> SearchMovies(string plot, int? releasedFrom, int? releasedTo, string? genre, CancellationToken ct);

    private readonly SearchToolOptions options;
    private readonly JsonElement schema;

    public SearchMoviesFunction(SearchMovies search, SearchToolOptions options)
        : base(AIFunctionFactory.Create(search))
    {
        this.options = options;
        schema = DescribeParameters(InnerFunction.JsonSchema, new()
        {
            ["plot"] = options.PlotDescription,
            ["releasedFrom"] = options.ReleasedFromDescription,
            ["releasedTo"] = options.ReleasedToDescription,
            ["genre"] = options.GenreDescription,
        });
    }

    public override string Name => options.Name;

    public override string Description => options.Description;

    public override JsonElement JsonSchema => schema;

    private static JsonElement DescribeParameters(JsonElement original, Dictionary<string, string> descriptions)
    {
        var node = JsonNode.Parse(original.GetRawText())!;
        var properties = node["properties"]?.AsObject() ?? throw new InvalidOperationException("Tool schema has no properties.");
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
