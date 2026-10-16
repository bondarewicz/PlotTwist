namespace PlotTwist.UnitTests.Fakes;

using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using PlotTwist.Domain.Embeddings;
using PlotTwist.Domain.Movies;
using PlotTwist.Domain.Search;

// Movies and their embeddings in memory, with the same pending rule as the real store. Thread-safe like the real
// store, because the embed job saves batches in parallel.
internal sealed class InMemoryMovieStore : IMovieRepository, IMovieEmbeddingRepository
{
    private readonly Lock gate = new();
    private readonly Dictionary<MovieId, Movie> movies = [];
    private readonly Dictionary<(MovieId, string), MovieEmbedding> embeddings = [];

    public MovieCriteria? LastCriteria { get; private set; }

    public int SaveCalls { get; private set; }

    public Task<int> Upsert(IReadOnlyCollection<Movie> batch, CancellationToken ct = default)
    {
        lock (gate)
        {
            var created = batch.Count(m => !movies.ContainsKey(m.Id));
            foreach (var movie in batch)
                movies[movie.Id] = movie;
            return Task.FromResult(created);
        }
    }

    public Task<IReadOnlyList<Movie>> Get(IReadOnlyCollection<MovieId> ids, CancellationToken ct = default)
    {
        lock (gate)
            return Task.FromResult<IReadOnlyList<Movie>>(ids.Select(id => movies[id]).ToList());
    }

    public Task<int> Count(CancellationToken ct = default)
    {
        lock (gate)
            return Task.FromResult(movies.Count);
    }

    public Task<IReadOnlyList<MovieId>> ListPending(EmbeddingModel model, CancellationToken ct = default)
    {
        lock (gate)
            return Task.FromResult<IReadOnlyList<MovieId>>(
                movies
                    .Values.Where(m =>
                        !embeddings.TryGetValue((m.Id, model.Name), out var e) || e.TextHash != m.SearchableTextHash
                    )
                    .Select(m => m.Id)
                    .ToList()
            );
    }

    public Task Save(IReadOnlyCollection<MovieEmbedding> batch, CancellationToken ct = default)
    {
        lock (gate)
        {
            SaveCalls++;
            foreach (var embedding in batch)
                embeddings[(embedding.MovieId, embedding.Model.Name)] = embedding;
        }

        return Task.CompletedTask;
    }

    public Task<int> Count(EmbeddingModel model, CancellationToken ct = default)
    {
        lock (gate)
            return Task.FromResult(embeddings.Keys.Count(k => k.Item2 == model.Name));
    }

    public Task<IReadOnlyList<MovieMatch>> FindSimilar(
        Embedding plot,
        EmbeddingModel model,
        MovieCriteria criteria,
        int top,
        CancellationToken ct = default
    )
    {
        lock (gate)
        {
            LastCriteria = criteria;
            IReadOnlyList<MovieMatch> matches = embeddings
                .Values.Where(e => e.Model == model && criteria.IsSatisfiedBy(movies[e.MovieId]))
                .Select(e => new MovieMatch(
                    movies[e.MovieId],
                    TensorPrimitives.CosineSimilarity(plot.Values.Span, e.Vector.Values.Span)
                ))
                .OrderByDescending(m => m.Similarity)
                .Take(top)
                .ToList();
            return Task.FromResult(matches);
        }
    }

    public async IAsyncEnumerable<(Movie Movie, Embedding Vector)> ListAll(
        EmbeddingModel model,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        List<(Movie Movie, Embedding Vector)> snapshot;
        lock (gate)
            snapshot = embeddings
                .Values.Where(e => e.Model == model)
                .Select(e => (movies[e.MovieId], e.Vector))
                .ToList();
        foreach (var row in snapshot)
            yield return row;
        await Task.CompletedTask;
    }
}
