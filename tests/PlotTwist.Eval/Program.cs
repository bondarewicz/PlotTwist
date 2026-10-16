// Step 4: measure retrieval instead of eyeballing it.
//
//   dotnet run --project tests/PlotTwist.Eval
//   dotnet run --project tests/PlotTwist.Eval -- --k 10
//
// Each case describes a well-known film WITHOUT naming it, the way a person who forgot the title would.
// Recall@k = share of cases where the right film is in the top k. MRR = mean of 1/rank (1.0 = always first).
// Change something (embedding model, the text in MoviePlot.ToEmbeddingText, the nomic prefixes, a filter)
// and re-run: if the numbers don't move, the change didn't matter.

using PlotTwist.Core;
using PlotTwist.Core.Search;

var k = args is ["--k", var kArg] ? int.Parse(kArg) : 5;

EvalCase[] cases =
[
    new("a weatherman is stuck living the same day in a small town over and over", "Groundhog Day", 1993),
    new("a hacker learns that reality is a computer simulation run by machines", "The Matrix", 1999),
    new("scientists clone dinosaurs for a theme park and they escape", "Jurassic Park", 1993),
    new("a poor artist and a rich girl fall in love on a ship that hits an iceberg", "Titanic", 1997),
    new("a cowboy toy gets jealous when a new space ranger toy becomes the favourite", "Toy Story", 1995),
    new("a giant shark terrorises a beach town and a police chief hunts it", "Jaws", 1975),
    new("a teenager travels back in time in a car and must get his parents to fall in love", "Back to the Future", 1985),
    new("thieves plant an idea in someone's mind by entering their dreams", "Inception", 2010),
    new("a man discovers his whole life is a reality TV show filmed in a fake town", "The Truman Show", 1998),
    new("a boy left behind by his family at christmas defends the house from burglars", "Home Alone", 1990),
    new("a clownfish father crosses the ocean to find his son", "Finding Nemo", 2003),
    new("an old man ties balloons to his house and flies to south america", "Up", 2009),
    new("a cop fights terrorists who take over a skyscraper on christmas eve", "Die Hard", 1988),
    new("a banker wrongly jailed for murder escapes prison after decades", "The Shawshank Redemption", 1994),
    new("a stranded astronaut grows potatoes on mars to survive", "The Martian", 2015),
    new("an oxygen tank explodes and the crew must improvise to get home from the moon", "Apollo 13", 1995),
    new("an FBI trainee asks an imprisoned cannibal psychiatrist for help catching a killer", "The Silence of the Lambs", 1991),
    new("a divorced father dresses up as an old british nanny to see his kids", "Mrs. Doubtfire", 1993),
    new("a bomb on a bus will explode if it drops below fifty miles an hour", "Speed", 1994),
    new("a man with no short-term memory hunts his wife's killer using tattoos and notes", "Memento", 2000),
    // Deliberately vague: plot alone is ambiguous, the filter is what makes it findable.
    new("astronauts on a dangerous mission in space", "Apollo 13", 1995, new MovieFilter(1990, 1999, "drama")),
    new("a robot falls in love", "WALL-E", 2008, new MovieFilter(2000, 2010, "animated")),
    new("a man visits his girlfriend's family and something is very wrong", "Get Out", 2017, new MovieFilter(Genre: "horror")),
];

var search = MovieSearch.Create(PlotTwistSettings.Load());
var top = Math.Max(k, 10);
var results = new List<(EvalCase Case, int? Rank, string First)>();

foreach (var c in cases)
{
    var hits = await search.SearchAsync(c.Query, c.Filter, top);
    var index = hits.ToList().FindIndex(h => h.Movie.Title == c.Title && h.Movie.Year == c.Year);
    results.Add((c, index < 0 ? null : index + 1, hits.Count > 0 ? hits[0].Movie.ToString() : "-"));
}

Console.WriteLine($"{"rank",4}  {"expected",-30} {"filter",-38} top hit");
foreach (var (c, rank, first) in results)
{
    Console.ForegroundColor = rank switch { 1 => ConsoleColor.Green, <= 5 => ConsoleColor.Yellow, _ => ConsoleColor.Red };
    Console.WriteLine($"{rank?.ToString() ?? $">{top}",4}  {c.Title,-30} {(c.Filter?.ToString() ?? ""),-38} {first}");
    Console.ResetColor();
}

double Recall(int n) => results.Count(r => r.Rank <= n) / (double)results.Count;
var mrr = results.Average(r => r.Rank is { } rank ? 1.0 / rank : 0);
Console.WriteLine();
Console.WriteLine($"recall@1 {Recall(1):P0}   recall@{k} {Recall(k):P0}   recall@{top} {Recall(top):P0}   MRR {mrr:F3}   ({results.Count} cases)");

internal sealed record EvalCase(string Query, string Title, int Year, MovieFilter? Filter = null);
