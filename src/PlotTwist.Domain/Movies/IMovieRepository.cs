namespace PlotTwist.Domain.Movies;

public interface IMovieRepository
{
    Task<int> Upsert(IReadOnlyCollection<Movie> movies, CancellationToken ct = default);

    Task<IReadOnlyList<Movie>> Get(IReadOnlyCollection<MovieId> ids, CancellationToken ct = default);

    Task<int> Count(CancellationToken ct = default);
}
