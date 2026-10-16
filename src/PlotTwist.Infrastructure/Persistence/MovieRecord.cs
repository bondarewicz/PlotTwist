namespace PlotTwist.Infrastructure.Persistence;

// Persistence model for the pgvector connector. The table shape lives in MovieRecordDefinition rather than in
// attributes, because the vector size comes from configuration (it depends on the embedding model).
public sealed class MovieRecord
{
    public int Id { get; set; }

    public string Title { get; set; } = "";

    public int Year { get; set; }

    public List<string> Genres { get; set; } = [];

    public string Origin { get; set; } = "";

    public string Director { get; set; } = "";

    public string Summary { get; set; } = "";

    public string WikiUrl { get; set; } = "";

    public ReadOnlyMemory<float> Embedding { get; set; }
}
