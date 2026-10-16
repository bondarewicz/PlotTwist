namespace PlotTwist.IntegrationTests;

using System.Text.Json;
using Microsoft.Extensions.AI;
using PlotTwist.Infrastructure.Assistant;
using PlotTwist.Infrastructure.Configuration;

// The tool the model calls: its schema comes from configuration, and arguments that don't bind are returned to
// the model rather than failing the request. No database needed.
public sealed class SearchMoviesFunctionTests
{
    private static readonly SearchToolOptions Options = new()
    {
        Name = "search_movies",
        Description = "Search films.",
        PlotDescription = "What happens.",
        ReleasedFromDescription = "Earliest year.",
        ReleasedToDescription = "Latest year.",
        GenreDescription = "One genre.",
    };

    [Fact]
    public void Name_description_and_parameter_descriptions_come_from_configuration()
    {
        var function = new SearchMoviesFunction((_, _, _, _, _) => Task.FromResult(""), Options);
        var properties = function.JsonSchema.GetProperty("properties");

        Assert.Equal("search_movies", function.Name);
        Assert.Equal("Search films.", function.Description);
        Assert.Equal("Earliest year.", properties.GetProperty("releasedFrom").GetProperty("description").GetString());
        Assert.Equal("One genre.", properties.GetProperty("genre").GetProperty("description").GetString());
        Assert.Equal(["plot"], function.JsonSchema.GetProperty("required").EnumerateArray().Select(r => r.GetString()));
    }

    [Fact]
    public async Task Arguments_that_do_not_bind_go_back_to_the_model_instead_of_failing_the_request()
    {
        var function = new SearchMoviesFunction((_, _, _, _, _) => Task.FromResult("searched"), Options);

        var result = await function.InvokeAsync(
            new AIFunctionArguments { ["plot"] = "astronauts", ["releasedFrom"] = JsonSerializer.SerializeToElement("1990s") },
            TestContext.Current.CancellationToken);

        Assert.StartsWith("Invalid arguments for search_movies", Assert.IsType<string>(result is JsonElement e ? e.GetString() : result));
    }

    [Fact]
    public async Task Valid_arguments_reach_the_search()
    {
        int? receivedFrom = null;
        var function = new SearchMoviesFunction((_, from, _, _, _) => { receivedFrom = from; return Task.FromResult("searched"); }, Options);

        await function.InvokeAsync(
            new AIFunctionArguments { ["plot"] = "astronauts", ["releasedFrom"] = JsonSerializer.SerializeToElement(1990) },
            TestContext.Current.CancellationToken);

        Assert.Equal(1990, receivedFrom);
    }
}
