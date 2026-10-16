namespace PlotTwist.Application.Import;

using System.Globalization;
using System.Text.RegularExpressions;
using Abstractions;
using Domain;
using Domain.Movies;

// Turns raw source rows into films, deciding what the source actually means. A film is its Wikipedia page,
// and the source repeats pages in two very different ways:
//
// - Same title and year on several rows: a co-production listed once per country. One film, several origins.
// - Different titles or years on one page: a scraping error - e.g. "The Other Side of the Door" 1916 and
//   2016 both carry the 2016 plot. If the URL names the year ("..._(2016_film)") the matching row wins;
//   otherwise the plot can't be attributed, and the whole group is rejected rather than indexed wrongly.
//
// Every row that doesn't become (part of) a film is returned as a reject with its reason - never silently dropped.
public static partial class MovieImportPolicy
{
    public static ImportDecision Decide(IEnumerable<RawMovieRow> rows)
    {
        var movies = new List<Movie>();
        var rejects = new List<MovieReject>();

        foreach (var page in rows.GroupBy(r => PageKey(r.WikiPage)))
        {
            var candidates = page.ToList();
            if (page.Key.Length == 0)
            {
                rejects.AddRange(candidates.Select(r => Reject(r, "No Wikipedia page, so the film has no identity.")));
                continue;
            }

            var (chosen, conflictReason) = Resolve(candidates);
            if (chosen is null)
            {
                rejects.AddRange(candidates.Select(r => Reject(r, conflictReason!)));
                continue;
            }

            rejects.AddRange(
                candidates
                    .Except(chosen)
                    .Select(r =>
                        Reject(
                            r,
                            $"Shares {r.WikiPage} with '{chosen[0].Title}' ({chosen[0].ReleaseYear}), whose year the page names."
                        )
                    )
            );

            try
            {
                movies.Add(ToMovie(chosen));
            }
            catch (DomainException e)
            {
                rejects.AddRange(chosen.Select(r => Reject(r, e.Message)));
            }
        }

        return new ImportDecision(movies, rejects);
    }

    // The rows that describe the page's film, or null and why not.
    private static (IReadOnlyList<RawMovieRow>? Chosen, string? Reason) Resolve(IReadOnlyList<RawMovieRow> rows)
    {
        if (rows.Select(Identity).Distinct().Count() == 1)
            return (rows, null);

        if (YearInUrl().Match(rows[0].WikiPage) is { Success: true } match)
        {
            var named = rows.Where(r => r.ReleaseYear.Trim() == match.Groups["year"].Value).ToList();
            if (named.Count > 0 && named.Select(Identity).Distinct().Count() == 1)
                return (named, null);
        }

        var variants = string.Join("; ", rows.Select(r => $"'{r.Title.Trim()}' ({r.ReleaseYear.Trim()})").Distinct());
        return (
            null,
            $"{rows.Count} rows share {rows[0].WikiPage} but describe different films ({variants}); the plot can't be attributed."
        );
    }

    private static Movie ToMovie(IReadOnlyList<RawMovieRow> rows)
    {
        var first = rows[0];
        if (!int.TryParse(first.ReleaseYear, NumberStyles.None, CultureInfo.InvariantCulture, out var year))
            throw new DomainException($"Release year '{first.ReleaseYear}' is not a year.");
        if (!Uri.TryCreate(first.WikiPage.Trim(), UriKind.Absolute, out var url))
            throw new DomainException($"'{first.WikiPage}' is not a valid URL.");

        return new Movie(
            url,
            first.Title,
            ReleaseYear.Create(year),
            Genre.ParseList(first.Genre),
            rows.Select(r => r.Origin).ToList(),
            first.Director,
            first.PlotSummary
        );
    }

    private static (string, string) Identity(RawMovieRow row) =>
        (row.Title.Trim().ToLowerInvariant(), row.ReleaseYear.Trim());

    private static string PageKey(string wikiPage) =>
        Uri.TryCreate(wikiPage.Trim(), UriKind.Absolute, out var url)
            ? url.GetLeftPart(UriPartial.Path).TrimEnd('/').ToLowerInvariant()
            : "";

    private static MovieReject Reject(RawMovieRow row, string reason) =>
        new(row.RowNumber, row.Title.Trim(), row.ReleaseYear.Trim(), reason);

    [GeneratedRegex(@"\((?<year>\d{4})_film\)")]
    private static partial Regex YearInUrl();
}

public sealed record ImportDecision(IReadOnlyList<Movie> Movies, IReadOnlyList<MovieReject> Rejects);

public sealed record MovieReject(int RowNumber, string Title, string ReleaseYear, string Reason);
