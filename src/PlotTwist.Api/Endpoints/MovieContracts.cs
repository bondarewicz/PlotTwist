namespace PlotTwist.Api.Endpoints;

using System.Globalization;
using Application.Assistant;
using Application.Search;
using Domain.Search;

public sealed record SearchMoviesRequest(
    string Plot,
    int? ReleasedFrom = null,
    int? ReleasedTo = null,
    string? Genre = null,
    int? Top = null
);

public sealed record AskRequest(string Question, AssistantMode Mode = AssistantMode.Tool);

public sealed record SearchMoviesResponse(
    SearchQuery Query,
    IReadOnlyList<MatchedMovie> Matches,
    SearchDiagnostics Metadata
);

public sealed record SearchQuery(string Plot, SearchCriteria Criteria, int Top);

public sealed record SearchCriteria(int? ReleasedFrom, int? ReleasedTo, string? Genre, bool Applied, string? Sql);

public sealed record MatchedMovie(
    int Rank,
    Guid Id,
    string Title,
    int ReleaseYear,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Origins,
    string Director,
    string PlotSummary,
    string? WikipediaUrl,
    double Similarity
);

public sealed record SearchDiagnostics(
    int MatchCount,
    string EmbeddingModel,
    int Dimensions,
    string SimilarityMetric,
    string IndexKind,
    Confidence Confidence,
    Timings Timings
);

public sealed record Confidence(double? TopSimilarity, double? Margin, bool Ambiguous, string Interpretation);

public sealed record Timings(double EmbeddingMs, double SearchMs, double TotalMs);

public sealed record CompareResponse(SearchQuery Query, IReadOnlyList<ComparisonStrategy> Strategies);

public sealed record ComparisonStrategy(
    string Strategy,
    double ElapsedMs,
    string Notes,
    IReadOnlyList<MatchedMovie> Matches
);

public sealed record AskResponse(
    string Answer,
    AssistantMode Mode,
    string ChatModel,
    bool Searched,
    IReadOnlyList<CitedMovie> Citations,
    IReadOnlyList<AssistantSearchSummary> Searches,
    TokenUsage? Usage,
    double ElapsedMs
);

public sealed record CitedMovie(string Title, int ReleaseYear, string? WikipediaUrl, double Similarity);

public sealed record AssistantSearchSummary(
    string RequestedBy,
    string Plot,
    SearchCriteria Criteria,
    int MatchCount,
    double? TopSimilarity,
    Timings Timings
);

public sealed record TokenUsage(long? InputTokens, long? OutputTokens);

public sealed record StatsResponse(
    int Movies,
    int Embedded,
    string EmbeddingModel,
    int Dimensions,
    bool VectorIndexExists,
    string ChatModel
);

internal static class MovieContractMapping
{
    public static SearchMoviesResponse ToResponse(this SearchMoviesResult result, int top) =>
        new(
            new SearchQuery(result.Plot.Value, result.Criteria.ToSearchCriteria(), top),
            result.Matches.ToMatchedMovies(),
            new SearchDiagnostics(
                result.Matches.Count,
                result.Metadata.EmbeddingModelName,
                result.Metadata.Dimensions,
                "cosine",
                "hnsw",
                result.Matches.ToConfidence(),
                result.Metadata.ToTimings()
            )
        );

    public static SearchCriteria ToSearchCriteria(this MovieCriteria criteria) =>
        new(
            criteria.ReleasedFrom?.Value,
            criteria.ReleasedTo?.Value,
            criteria.Genre?.Value,
            !criteria.IsEmpty,
            criteria.IsEmpty
                ? null
                : string.Join(
                    " AND ",
                    new[]
                    {
                        criteria.ReleasedFrom is { } from ? $"release_year >= {from}" : null,
                        criteria.ReleasedTo is { } to ? $"release_year <= {to}" : null,
                        criteria.Genre is { } genre ? $"genres @> ARRAY['{genre.Value.Replace("'", "''")}']" : null,
                    }.OfType<string>()
                )
        );

    public static IReadOnlyList<MatchedMovie> ToMatchedMovies(this IReadOnlyList<MovieMatch> matches) =>
        matches
            .Select(
                (m, i) =>
                    new MatchedMovie(
                        i + 1,
                        m.Movie.Id.Value,
                        m.Movie.Title,
                        m.Movie.ReleaseYear.Value,
                        m.Movie.Genres.Select(g => g.Value).ToList(),
                        m.Movie.Origins,
                        m.Movie.Director,
                        m.Movie.PlotSummary,
                        m.Movie.WikipediaPage.Url.ToString(),
                        Math.Round(m.Similarity, 4)
                    )
            )
            .ToList();

    public static Confidence ToConfidence(this IReadOnlyList<MovieMatch> matches)
    {
        var margin = MatchConfidence.Margin(matches);
        var ambiguous = MatchConfidence.IsAmbiguous(matches);
        var interpretation = matches.Count switch
        {
            0 => "No movies matched; loosen the criteria.",
            1 => "Only one movie matched the criteria.",
            _ when ambiguous => string.Create(
                CultureInfo.InvariantCulture,
                $"Ambiguous: the top two results are within {MatchConfidence.AmbiguousMargin} similarity of each other. Add detail or criteria."
            ),
            _ => "The top result stands out from the rest.",
        };
        return new Confidence(
            matches.Count > 0 ? Math.Round(matches[0].Similarity, 4) : null,
            margin is { } m ? Math.Round(m, 4) : null,
            ambiguous,
            interpretation
        );
    }

    public static Timings ToTimings(this SearchMetadata metadata) =>
        new(
            Math.Round(metadata.EmbeddingTime.TotalMilliseconds, 1),
            Math.Round(metadata.SearchTime.TotalMilliseconds, 1),
            Math.Round(metadata.TotalTime.TotalMilliseconds, 1)
        );

    public static AskResponse ToResponse(this AssistantAnswer answer) =>
        new(
            answer.Text,
            answer.Mode,
            answer.ChatModel,
            answer.Searches.Count > 0,
            answer.Searches.ToCitations(),
            answer.Searches.ToSearches(),
            answer.Usage is { } u ? new TokenUsage(u.InputTokens, u.OutputTokens) : null,
            Math.Round(answer.Elapsed.TotalMilliseconds, 1)
        );

    private static IReadOnlyList<CitedMovie> ToCitations(this IReadOnlyList<AssistantSearch> searches) =>
        searches
            .SelectMany(s => s.Result.Matches)
            .GroupBy(m => m.Movie.Id)
            .Select(g => g.MaxBy(m => m.Similarity)!)
            .OrderByDescending(m => m.Similarity)
            .Select(m => new CitedMovie(
                m.Movie.Title,
                m.Movie.ReleaseYear.Value,
                m.Movie.WikipediaPage.Url.ToString(),
                Math.Round(m.Similarity, 4)
            ))
            .ToList();

    private static IReadOnlyList<AssistantSearchSummary> ToSearches(this IReadOnlyList<AssistantSearch> searches) =>
        searches
            .Select(s => new AssistantSearchSummary(
                s.RequestedBy,
                s.Result.Plot.Value,
                s.Result.Criteria.ToSearchCriteria(),
                s.Result.Matches.Count,
                s.Result.Matches.Count > 0 ? Math.Round(s.Result.Matches[0].Similarity, 4) : null,
                s.Result.Metadata.ToTimings()
            ))
            .ToList();
}
