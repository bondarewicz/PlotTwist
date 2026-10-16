namespace PlotTwist.Domain.Search;

using Movies;

public sealed record MovieMatch(Movie Movie, double Similarity);
