namespace PlotTwist.Infrastructure.Assistant;

using System.Text.Json;
using System.Text.Json.Nodes;
using Configuration;
using Microsoft.Extensions.AI;

internal sealed class SearchMoviesTool : DelegatingAIFunction
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

    public SearchMoviesTool(SearchMovies search, SearchToolOptions options)
        // AIFunctionFactory names schema parameters from this lambda, not from the delegate type.
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
                ["plot"] = options.PlotParameterDescription,
                ["releasedFrom"] = options.ReleasedFromParameterDescription,
                ["releasedTo"] = options.ReleasedToParameterDescription,
                ["genre"] = options.GenreParameterDescription,
            }
        );
    }

    public override string Name => options.Name;

    public override string Description => options.Description;

    public override JsonElement JsonSchema => schema;

    // Agent Framework rethrows binding errors; returning them as the tool result lets the model retry instead.
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
            property["description"] = description;
        }

        return JsonSerializer.SerializeToElement(node);
    }
}
