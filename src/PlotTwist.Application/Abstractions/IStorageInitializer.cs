namespace PlotTwist.Application.Abstractions;

// Creates the table, vector index and filter indexes if they don't exist yet. Idempotent.
public interface IStorageInitializer
{
    Task EnsureCreated(CancellationToken ct = default);
}
