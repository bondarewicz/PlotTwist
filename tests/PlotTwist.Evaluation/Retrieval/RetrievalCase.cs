namespace PlotTwist.Evaluation.Retrieval;

using Domain.Search;

// A film described the way someone who forgot the title would, and the film they meant.
public sealed record RetrievalCase(
    string Name,
    string Description,
    string ExpectedTitle,
    int ExpectedYear,
    MovieCriteria? Criteria = null
)
{
    public override string ToString() => Name;
}

public static class RetrievalCases
{
    // The last three are deliberately vague: plot alone is ambiguous, and the criteria make them findable.
    public static IReadOnlyList<RetrievalCase> All { get; } =
    [
        new(
            "groundhog-day",
            "a weatherman is stuck living the same day in a small town over and over",
            "Groundhog Day",
            1993
        ),
        new("the-matrix", "a hacker learns that reality is a computer simulation run by machines", "The Matrix", 1999),
        new("jurassic-park", "scientists clone dinosaurs for a theme park and they escape", "Jurassic Park", 1993),
        new("titanic", "a poor artist and a rich girl fall in love on a ship that hits an iceberg", "Titanic", 1997),
        new(
            "toy-story",
            "a cowboy toy gets jealous when a new space ranger toy becomes the favourite",
            "Toy Story",
            1995
        ),
        new("jaws", "a giant shark terrorises a beach town and a police chief hunts it", "Jaws", 1975),
        new(
            "back-to-the-future",
            "a teenager travels back in time in a car and must get his parents to fall in love",
            "Back to the Future",
            1985
        ),
        new("inception", "thieves plant an idea in someone's mind by entering their dreams", "Inception", 2010),
        new(
            "the-truman-show",
            "a man discovers his whole life is a reality TV show filmed in a fake town",
            "The Truman Show",
            1998
        ),
        new(
            "home-alone",
            "a boy left behind by his family at christmas defends the house from burglars",
            "Home Alone",
            1990
        ),
        new("finding-nemo", "a clownfish father crosses the ocean to find his son", "Finding Nemo", 2003),
        new("up", "an old man ties balloons to his house and flies to south america", "Up", 2009),
        new("die-hard", "a cop fights terrorists who take over a skyscraper on christmas eve", "Die Hard", 1988),
        new(
            "shawshank",
            "a banker wrongly jailed for murder escapes prison after decades",
            "The Shawshank Redemption",
            1994
        ),
        new("the-martian", "a stranded astronaut grows potatoes on mars to survive", "The Martian", 2015),
        new(
            "apollo-13",
            "an oxygen tank explodes and the crew must improvise to get home from the moon",
            "Apollo 13",
            1995
        ),
        new(
            "silence-of-the-lambs",
            "an FBI trainee asks an imprisoned cannibal psychiatrist for help catching a killer",
            "The Silence of the Lambs",
            1991
        ),
        new(
            "mrs-doubtfire",
            "a divorced father dresses up as an old british nanny to see his kids",
            "Mrs. Doubtfire",
            1993
        ),
        new("speed", "a bomb on a bus will explode if it drops below fifty miles an hour", "Speed", 1994),
        new(
            "memento",
            "a man with no short-term memory hunts his wife's killer using tattoos and notes",
            "Memento",
            2000
        ),
        new(
            "vague-apollo-13",
            "astronauts on a dangerous mission in space",
            "Apollo 13",
            1995,
            MovieCriteria.Create(1990, 1999, "drama")
        ),
        new("vague-wall-e", "a robot falls in love", "WALL-E", 2008, MovieCriteria.Create(2000, 2010, "animated")),
        new(
            "vague-get-out",
            "a man visits his girlfriend's family and something is very wrong",
            "Get Out",
            2017,
            MovieCriteria.Create(genre: "horror")
        ),
    ];
}
