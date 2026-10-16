namespace PlotTwist.Api.Endpoints;

using Application.Abstractions;
using Application.Assistant;
using Application.Search;
using Domain.Embeddings;
using Domain.Movies;
using Domain.Search;
using Infrastructure;
using Infrastructure.Configuration;

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
                "Ask the Agent Framework assistant; the response includes the searches it ran and the films it drew on."
            );

        movies.MapGet("/stats", Stats).WithSummary("How many films are indexed and which models are configured.");

        return app;
    }

    private static async Task<SearchMoviesResponse> Search(
        SearchMoviesRequest request,
        FindMoviesHandler handler,
        CancellationToken ct
    )
    {
        var top = request.Top ?? FindMovies.DefaultTop;
        var result = await handler.Handle(new FindMovies(ToDescription(request), ToCriteria(request), top), ct);
        return result.ToResponse(top);
    }

    private static async Task<CompareResponse> Compare(
        SearchMoviesRequest request,
        CompareSearchStrategiesHandler handler,
        CancellationToken ct
    )
    {
        var top = request.Top ?? FindMovies.DefaultTop;
        var comparison = await handler.Handle(
            new CompareSearchStrategies(ToDescription(request), ToCriteria(request), top),
            ct
        );
        return new CompareResponse(
            new SearchQueryDto(comparison.Description.Value, comparison.Criteria.ToDto(), top),
            comparison
                .Strategies.Select(s => new StrategyDto(
                    s.Strategy,
                    Math.Round(s.Elapsed.TotalMilliseconds, 1),
                    s.Notes,
                    s.Matches.ToDtos()
                ))
                .ToList()
        );
    }

    private static async Task<AskResponse> Ask(AskRequest request, AskAssistantHandler handler, CancellationToken ct) =>
        (await handler.Handle(new AskAssistant(request.Question, request.Mode), ct)).ToResponse();

    private static async Task<StatsResponse> Stats(
        IMovieRepository movies,
        IMovieEmbeddingRepository embeddings,
        IVectorIndex index,
        IPlotEmbedder embedder,
        PlotTwistOptions options,
        CancellationToken ct
    ) =>
        new(
            await movies.Count(ct),
            await embeddings.Count(embedder.Model, ct),
            $"{options.Embeddings.Provider}:{embedder.Model.Name}",
            embedder.Model.Dimensions,
            (await index.Status(embedder.Model, ct)).Exists,
            $"{options.Chat.Provider}:{options.Chat.Model}"
        );

    private static PlotDescription ToDescription(SearchMoviesRequest request) =>
        PlotDescription.Create(request.Description);

    private static MovieCriteria ToCriteria(SearchMoviesRequest request) =>
        MovieCriteria.Create(request.ReleasedFrom, request.ReleasedTo, request.Genre);
}
