using Microsoft.Extensions.VectorData;

namespace PlotTwist.Core.Movies;

/// <summary>
/// One film = one row in pgvector. The summaries are short (~380 chars), so there is no chunking:
/// the whole summary is embedded as a single vector.
/// </summary>
public sealed class MoviePlot
{
    public const int Dimensions = 768;

    [VectorStoreKey]
    public int Id { get; set; }

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Title { get; set; } = "";

    [VectorStoreData(IsIndexed = true)]
    public int Year { get; set; }

    /// <summary>Normalised genre tags (the raw column is free text like "sci-fi, comedy").</summary>
    [VectorStoreData(IsIndexed = true)]
    public List<string> Genres { get; set; } = [];

    [VectorStoreData(IsIndexed = true)]
    public string Origin { get; set; } = "";

    [VectorStoreData]
    public string Director { get; set; } = "";

    [VectorStoreData]
    public string Summary { get; set; } = "";

    [VectorStoreData]
    public string WikiUrl { get; set; } = "";

    [VectorStoreVector(Dimensions, DistanceFunction = DistanceFunction.CosineSimilarity, IndexKind = IndexKind.Hnsw)]
    public ReadOnlyMemory<float> Embedding { get; set; }

    /// <summary>The text that gets embedded. Title and genre help as much as the plot does.</summary>
    public string ToEmbeddingText() => $"{Title} ({Year}). {string.Join(", ", Genres)}. {Summary}";

    public override string ToString() => $"{Title} ({Year}) [{string.Join(", ", Genres)}]";
}
