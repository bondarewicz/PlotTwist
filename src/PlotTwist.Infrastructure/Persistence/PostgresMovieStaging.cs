namespace PlotTwist.Infrastructure.Persistence;

using System.Globalization;
using System.Runtime.CompilerServices;
using Application.Abstractions;
using CsvHelper;
using Dapper;
using Npgsql;
using NpgsqlTypes;

public sealed class PostgresMovieStaging(NpgsqlDataSource dataSource) : IMovieStaging
{
    private static readonly string[] SourceColumns =
    [
        "Release Year",
        "Title",
        "Origin/Ethnicity",
        "Director",
        "Cast",
        "Genre",
        "Wiki Page",
        "Plot",
        "PlotSummary",
    ];

    public async Task<int> Stage(SourceSnapshot snapshot, long runId, CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await connection.ExecuteAsync(
            new CommandDefinition("TRUNCATE staging.movies_raw", transaction: transaction, cancellationToken: ct)
        );

        using var reader = new StreamReader(snapshot.LocalPath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        await ValidateHeader(csv, snapshot);
        var rows = await CopyRows(connection, csv, runId, ct);

        await transaction.CommitAsync(ct);
        return rows;
    }

    private static async Task ValidateHeader(CsvReader csv, SourceSnapshot snapshot)
    {
        await csv.ReadAsync();
        csv.ReadHeader();
        var missing = SourceColumns.Except(csv.HeaderRecord ?? []).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"{snapshot.Name}@{snapshot.Revision} is missing columns: {string.Join(", ", missing)}."
            );
    }

    private static async Task<int> CopyRows(
        NpgsqlConnection connection,
        CsvReader csv,
        long runId,
        CancellationToken ct
    )
    {
        var rows = 0;
        await using var writer = await connection.BeginBinaryImportAsync(
            """
            COPY staging.movies_raw (run_id, row_number, release_year, title, origin, director, cast_list, genre, wiki_page, plot, plot_summary)
            FROM STDIN (FORMAT BINARY)
            """,
            ct
        );
        while (await csv.ReadAsync())
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(runId, NpgsqlDbType.Bigint, ct);
            await writer.WriteAsync(++rows, NpgsqlDbType.Integer, ct);
            foreach (var column in SourceColumns)
                await writer.WriteAsync(csv.GetField(column) ?? "", NpgsqlDbType.Text, ct);
        }

        await writer.CompleteAsync(ct);
        return rows;
    }

    public async IAsyncEnumerable<RawMovieRow> Read(long runId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        // QueryUnbufferedAsync has no CommandDefinition overload, so ct only cancels enumeration, not the command.
        var rows = connection.QueryUnbufferedAsync<RawMovieRow>(
            """
            SELECT row_number, release_year, title, origin, director, cast_list AS cast, genre, wiki_page, plot, plot_summary
            FROM staging.movies_raw WHERE run_id = @RunId ORDER BY row_number
            """,
            new { RunId = runId }
        );
        await foreach (var row in rows.WithCancellation(ct))
            yield return row;
    }
}
