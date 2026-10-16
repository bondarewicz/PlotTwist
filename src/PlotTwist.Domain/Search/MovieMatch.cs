namespace PlotTwist.Domain.Search;

using PlotTwist.Domain.Movies;

// Cosine similarity, -1..1; in practice 0.4-0.9 for this model. Only comparable within one embedding model.
public sealed record MovieMatch(Movie Movie, double Similarity);
