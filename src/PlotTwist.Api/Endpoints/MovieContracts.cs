namespace PlotTwist.Api.Endpoints;

using System.Globalization;
using Application.Assistant;
using Application.Search;
using Domain.Search;

// Requests -----------------------------------------------------------------------------------------------

public sealed record SearchMoviesRequest(
    string Description,
    int? ReleasedFrom = null,
    int? ReleasedTo = null,
    string? Genre = null,
    int? Top = null
);

public sealed record AskRequest(string Question, AssistantMode Mode = AssistantMode.Tool);

// Responses ----------------------------------------------------------------------------------------------

public sealed record SearchMoviesResponse(
    SearchQueryDto Query,
    IReadOnlyList<MovieResultDto> Results,
    SearchMetadataDto Metadata
);

public sealed record SearchQueryDto(string Description, CriteriaDto Criteria, int Top);

// Filter is the WHERE clause the criteria become in Postgres, to show that filtering happens in the database.
public sealed record CriteriaDto(int? ReleasedFrom, int? ReleasedTo, string? Genre, bool Applied, string? Filter);

public sealed record MovieResultDto(
    int Rank,
    Guid Id,
    string Title,
    int Year,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Origins,
    string Director,
    string Summary,
    string? WikipediaUrl,
    double Similarity
);

public sealed record SearchMetadataDto(
    int ResultCount,
    string EmbeddingModel,
    int Dimensions,
    string Distance,
    string Index,
    ConfidenceDto Confidence,
    TimingsDto Timings
);

// TopSimilarity alone says little (it varies per model); the margin over the runner-up says whether the
// description singled out one film or fits several about equally.
public sealed record ConfidenceDto(double? TopSimilarity, double? Margin, bool Ambiguous, string Interpretation);

public sealed record TimingsDto(double EmbeddingMs, double SearchMs, double TotalMs);

public sealed record CompareResponse(SearchQueryDto Query, IReadOnlyList<StrategyDto> Strategies);

public sealed record StrategyDto(
    string Strategy,
    double ElapsedMs,
    string Notes,
    IReadOnlyList<MovieResultDto> Results
);

public sealed record AskResponse(
    string Answer,
    AssistantMode Mode,
    string ChatModel,
    bool Grounded,
    IReadOnlyList<SourceDto> Sources,
    IReadOnlyList<AgentSearchDto> Searches,
    UsageDto? Usage,
    double ElapsedMs
);

// Every film the answer could have drawn on, best similarity first, deduplicated across searches.
public sealed record SourceDto(string Title, int Year, string? WikipediaUrl, double Similarity);

public sealed record AgentSearchDto(
    string RequestedBy,
    string Description,
    CriteriaDto Criteria,
    int ResultCount,
    double? TopSimilarity,
    TimingsDto Timings
);

public sealed record UsageDto(long? InputTokens, long? OutputTokens);

// Embedded < Movies means the embed job hasn't caught up: some films can't be found yet.
public sealed record StatsResponse(
    int Movies,
    int Embedded,
    string EmbeddingModel,
    int Dimensions,
    bool VectorIndex,
    string ChatModel
);

// Mapping ------------------------------------------------------------------------------------------------

internal static class MovieContractMapping
{
    public static SearchMoviesResponse ToResponse(this FindMoviesResult result, int top) =>
        new(
            new SearchQueryDto(result.Description.Value, result.Criteria.ToDto(), top),
            result.Matches.ToDtos(),
            new SearchMetadataDto(
                result.Matches.Count,
                result.Metadata.EmbeddingModel,
                result.Metadata.Dimensions,
                "cosine",
                "hnsw",
                result.Matches.ToConfidence(),
                result.Metadata.ToTimings()
            )
        );

    public static CriteriaDto ToDto(this MovieCriteria criteria) =>
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
                        criteria.Genre is { } genre ? $"genres @> ARRAY['{genre}']" : null,
                    }.OfType<string>()
                )
        );

    public static IReadOnlyList<MovieResultDto> ToDtos(this IReadOnlyList<MovieMatch> matches) =>
        matches
            .Select(
                (m, i) =>
                    new MovieResultDto(
                        i + 1,
                        m.Movie.Id.Value,
                        m.Movie.Title,
                        m.Movie.ReleaseYear.Value,
                        m.Movie.Genres.Select(g => g.Value).ToList(),
                        m.Movie.Origins,
                        m.Movie.Director,
                        m.Movie.PlotSummary,
                        m.Movie.WikipediaUrl?.ToString(),
                        Math.Round(m.Similarity, 4)
                    )
            )
            .ToList();

    public static ConfidenceDto ToConfidence(this IReadOnlyList<MovieMatch> matches)
    {
        var margin = MatchConfidence.Margin(matches);
        var ambiguous = MatchConfidence.IsAmbiguous(matches);
        var interpretation = matches.Count switch
        {
            0 => "No films matched; loosen the criteria.",
            1 => "Only one film matched the criteria.",
            _ when ambiguous => string.Create(
                CultureInfo.InvariantCulture,
                $"Ambiguous: the top two results are within {MatchConfidence.AmbiguousMargin} similarity of each other. Add detail or criteria."
            ),
            _ => "The top result stands out from the rest.",
        };
        return new ConfidenceDto(
            matches.Count > 0 ? Math.Round(matches[0].Similarity, 4) : null,
            margin is { } m ? Math.Round(m, 4) : null,
            ambiguous,
            interpretation
        );
    }

    public static TimingsDto ToTimings(this SearchMetadata metadata) =>
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
            answer
                .Searches.SelectMany(s => s.Result.Matches)
                .GroupBy(m => m.Movie.Id)
                .Select(g => g.MaxBy(m => m.Similarity)!)
                .OrderByDescending(m => m.Similarity)
                .Select(m => new SourceDto(
                    m.Movie.Title,
                    m.Movie.ReleaseYear.Value,
                    m.Movie.WikipediaUrl?.ToString(),
                    Math.Round(m.Similarity, 4)
                ))
                .ToList(),
            answer
                .Searches.Select(s => new AgentSearchDto(
                    s.RequestedBy,
                    s.Result.Description.Value,
                    s.Result.Criteria.ToDto(),
                    s.Result.Matches.Count,
                    s.Result.Matches.Count > 0 ? Math.Round(s.Result.Matches[0].Similarity, 4) : null,
                    s.Result.Metadata.ToTimings()
                ))
                .ToList(),
            answer.Usage is { } u ? new UsageDto(u.InputTokens, u.OutputTokens) : null,
            Math.Round(answer.Elapsed.TotalMilliseconds, 1)
        );
}
