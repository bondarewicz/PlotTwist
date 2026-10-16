namespace PlotTwist.Infrastructure.Persistence;

using Microsoft.Extensions.VectorData;

// EnsureCollectionExistsAsync turns this into: integer key, text[] genres, btree indexes on the filter columns,
// a full-text index on the title, and an HNSW index on the vector using cosine distance.
internal static class MovieRecordDefinition
{
    public static VectorStoreCollectionDefinition Create(int dimensions) => new()
    {
        Properties =
        [
            new VectorStoreKeyProperty(nameof(MovieRecord.Id), typeof(int)),
            new VectorStoreDataProperty(nameof(MovieRecord.Title), typeof(string)) { IsFullTextIndexed = true },
            new VectorStoreDataProperty(nameof(MovieRecord.Year), typeof(int)) { IsIndexed = true },
            new VectorStoreDataProperty(nameof(MovieRecord.Genres), typeof(List<string>)) { IsIndexed = true },
            new VectorStoreDataProperty(nameof(MovieRecord.Origin), typeof(string)) { IsIndexed = true },
            new VectorStoreDataProperty(nameof(MovieRecord.Director), typeof(string)),
            new VectorStoreDataProperty(nameof(MovieRecord.Summary), typeof(string)),
            new VectorStoreDataProperty(nameof(MovieRecord.WikiUrl), typeof(string)),
            new VectorStoreVectorProperty(nameof(MovieRecord.Embedding), typeof(ReadOnlyMemory<float>), dimensions)
            {
                DistanceFunction = DistanceFunction.CosineSimilarity,
                IndexKind = IndexKind.Hnsw,
            },
        ],
    };
}
