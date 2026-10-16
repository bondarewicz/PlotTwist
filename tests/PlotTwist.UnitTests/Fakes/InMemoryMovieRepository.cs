namespace PlotTwist.UnitTests.Fakes;

using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using PlotTwist.Application.Abstractions;
using PlotTwist.Domain.Embeddings;
using PlotTwist.Domain.Movies;
using PlotTwist.Domain.Search;

internal sealed class InMemoryMovieRepository : IMovieRepository, IStorageInitializer
{
    private readonly Dictionary<MovieId, Movie> movies = [];

    public bool Created { get; private set; }

    public MovieCriteria? LastCriteria { get; private set; }

    public Task EnsureCreated(CancellationToken ct = default)
    {
        Created = true;
        return Task.CompletedTask;
    }

    public Task Save(IReadOnlyCollection<Movie> batch, CancellationToken ct = default)
    {
        foreach (var movie in batch)
            movies[movie.Id] = movie.IsEmbedded ? movie : throw new InvalidOperationException($"{movie} is not embedded.");
        return Task.CompletedTask;
    }

    public Task<int> Count(CancellationToken ct = default) => Task.FromResult(movies.Count);

    public Task<IReadOnlyList<MovieMatch>> FindSimilar(Embedding plot, MovieCriteria criteria, int top, CancellationToken ct = default)
    {
        LastCriteria = criteria;
        IReadOnlyList<MovieMatch> matches = movies.Values
            .Where(criteria.IsSatisfiedBy)
            .Select(m => new MovieMatch(m, TensorPrimitives.CosineSimilarity(plot.Values.Span, m.PlotEmbedding!.Values.Span)))
            .OrderByDescending(m => m.Similarity)
            .Take(top)
            .ToList();
        return Task.FromResult(matches);
    }

    public async IAsyncEnumerable<Movie> ListEmbedded([EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var movie in movies.Values)
            yield return movie;
        await Task.CompletedTask;
    }
}
