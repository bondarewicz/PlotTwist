namespace PlotTwist.Api.Endpoints;

using Application.Abstractions;
using Application.Assistant;
using Application.Search;
using Domain.Embeddings;
using Domain.Movies;
using Domain.Search;
using Microsoft.AspNetCore.Http.HttpResults;

public static class MovieEndpoints
{
    public static IEndpointRouteBuilder MapMovieEndpoints(this IEndpointRouteBuilder app)
    {
        var movies = app.MapGroup("/movies").WithTags("Movies");

        movies
            .MapPost("/search", Search)
            .WithSummary("Semantic search over plot summaries, with optional year and genre filters.");

        movies
            .MapPost("/search/compare", Compare)
            .WithSummary(
                "The same search done brute force in memory, with the pgvector index, and with index + filters."
            );

        movies
            .MapPost("/ask", Ask)
            .WithSummary(
                "Ask the Agent Framework assistant; the response includes the searches it ran and the movies it drew on."
            );

        movies.MapGet("/stats", Stats).WithSummary("How many movies are indexed and which models are configured.");

        return app;
    }

    private static async Task<Ok<SearchMoviesResponse>> Search(
        SearchMoviesRequest request,
        SearchMoviesHandler handler,
        CancellationToken ct
    )
    {
        var top = request.Top ?? SearchMovies.DefaultTop;
        var result = await handler.Handle(new SearchMovies(ToPlot(request), ToCriteria(request), top), ct);
        return TypedResults.Ok(result.ToResponse(top));
    }

    private static async Task<Ok<CompareResponse>> Compare(
        SearchMoviesRequest request,
        CompareSearchStrategiesHandler handler,
        CancellationToken ct
    )
    {
        var top = request.Top ?? SearchMovies.DefaultTop;
        var comparison = await handler.Handle(
            new CompareSearchStrategies(ToPlot(request), ToCriteria(request), top),
            ct
        );
        return TypedResults.Ok(
            new CompareResponse(
                new SearchQuery(comparison.Plot.Value, comparison.Criteria.ToSearchCriteria(), top),
                comparison
                    .Strategies.Select(s => new ComparisonStrategy(
                        s.Strategy,
                        Math.Round(s.Elapsed.TotalMilliseconds, 1),
                        s.Notes,
                        s.Matches.ToMatchedMovies()
                    ))
                    .ToList()
            )
        );
    }

    private static async Task<Ok<AskResponse>> Ask(
        AskRequest request,
        AskAssistantHandler handler,
        CancellationToken ct
    ) => TypedResults.Ok((await handler.Handle(new AskAssistant(request.Question, request.Mode), ct)).ToResponse());

    private static async Task<Ok<StatsResponse>> Stats(
        IMovieRepository movies,
        IMovieEmbeddingRepository embeddings,
        IVectorIndex index,
        IPlotEmbedder embedder,
        IMovieAssistant assistant,
        CancellationToken ct
    ) =>
        TypedResults.Ok(
            new StatsResponse(
                await movies.Count(ct),
                await embeddings.Count(embedder.Model, ct),
                $"{embedder.ProviderName}:{embedder.Model.Name}",
                embedder.Model.Dimensions,
                (await index.Status(embedder.Model, ct)).Exists,
                $"{assistant.ProviderName}:{assistant.Model}"
            )
        );

    private static PlotDescription ToPlot(SearchMoviesRequest request) => PlotDescription.Create(request.Plot);

    private static MovieCriteria ToCriteria(SearchMoviesRequest request) =>
        MovieCriteria.Create(request.ReleasedFrom, request.ReleasedTo, request.Genre);
}
