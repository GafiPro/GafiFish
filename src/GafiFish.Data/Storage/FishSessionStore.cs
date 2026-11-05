using System.Globalization;
using GafiFish.Core.Models;
using GafiFish.Core.Services;
using GafiFish.Data.Models;
using Microsoft.Data.Sqlite;

namespace GafiFish.Data.Storage;

/// <summary>
/// Local SQLite store for session metadata and OCR-recognised catches.
/// The database is kept under LocalAppData; no cloud account is involved.
/// </summary>
public sealed class FishSessionStore
{
    private readonly string _connectionString;

    public FishSessionStore(string? databasePath = null)
    {
        databasePath ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GafiFish",
            "sessions.db");

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS Sessions (
                Id TEXT NOT NULL PRIMARY KEY,
                StartedAt TEXT NOT NULL,
                EndedAt TEXT NULL,
                TotalCatches INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS FishCatches (
                Id TEXT NOT NULL PRIMARY KEY,
                SessionId TEXT NOT NULL,
                CaughtAt TEXT NOT NULL,
                Species TEXT NULL,
                Rarity INTEGER NULL,
                Mutation TEXT NULL,
                WeightKg REAL NULL,
                EstimatedValue REAL NULL,
                RecognitionConfidence REAL NOT NULL,
                FOREIGN KEY(SessionId) REFERENCES Sessions(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS IX_FishCatches_SessionId
                ON FishCatches(SessionId);

            CREATE INDEX IF NOT EXISTS IX_Sessions_StartedAt
                ON Sessions(StartedAt DESC);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Guid> StartSessionAsync(
        DateTimeOffset startedAt,
        CancellationToken cancellationToken = default)
    {
        var sessionId = Guid.NewGuid();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Sessions (Id, StartedAt, EndedAt, TotalCatches)
            VALUES ($id, $started, NULL, 0);
            """;
        command.Parameters.AddWithValue("$id", sessionId.ToString("D"));
        command.Parameters.AddWithValue("$started", FormatDate(startedAt));
        await command.ExecuteNonQueryAsync(cancellationToken);

        return sessionId;
    }

    public async Task SaveCatchAsync(
        Guid sessionId,
        FishCatch fishCatch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fishCatch);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = (SqliteTransaction)transaction;
            insert.CommandText = """
                INSERT INTO FishCatches (
                    Id, SessionId, CaughtAt, Species, Rarity, Mutation,
                    WeightKg, EstimatedValue, RecognitionConfidence)
                VALUES (
                    $id, $session, $caught, $species, $rarity, $mutation,
                    $weight, $value, $confidence);
                """;
            insert.Parameters.AddWithValue("$id", fishCatch.Id.ToString("D"));
            insert.Parameters.AddWithValue("$session", sessionId.ToString("D"));
            insert.Parameters.AddWithValue("$caught", FormatDate(fishCatch.CaughtAt));
            insert.Parameters.AddWithValue("$species", (object?)fishCatch.Species ?? DBNull.Value);
            insert.Parameters.AddWithValue("$rarity", fishCatch.Rarity.HasValue
                ? (object)(int)fishCatch.Rarity.Value
                : DBNull.Value);
            insert.Parameters.AddWithValue("$mutation", (object?)fishCatch.Mutation ?? DBNull.Value);
            insert.Parameters.AddWithValue("$weight", (object?)fishCatch.WeightKg ?? DBNull.Value);
            insert.Parameters.AddWithValue("$value", fishCatch.EstimatedValue.HasValue
                ? (object)(double)fishCatch.EstimatedValue.Value
                : DBNull.Value);
            insert.Parameters.AddWithValue("$confidence", fishCatch.RecognitionConfidence);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = (SqliteTransaction)transaction;
            update.CommandText = """
                UPDATE Sessions
                SET TotalCatches = TotalCatches + 1
                WHERE Id = $session AND EndedAt IS NULL;
                """;
            update.Parameters.AddWithValue("$session", sessionId.ToString("D"));
            var updated = await update.ExecuteNonQueryAsync(cancellationToken);
            if (updated != 1)
            {
                throw new InvalidOperationException("The session does not exist or has already ended.");
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task FinishSessionAsync(
        Guid sessionId,
        DateTimeOffset endedAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Sessions
            SET EndedAt = $ended
            WHERE Id = $id AND EndedAt IS NULL;
            """;
        command.Parameters.AddWithValue("$ended", FormatDate(endedAt));
        command.Parameters.AddWithValue("$id", sessionId.ToString("D"));

        var updated = await command.ExecuteNonQueryAsync(cancellationToken);
        if (updated != 1)
        {
            throw new InvalidOperationException("The session does not exist or has already ended.");
        }
    }

    public async Task<FishingSessionRecord?> GetSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, StartedAt, EndedAt, TotalCatches
            FROM Sessions
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", sessionId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var ended = reader.IsDBNull(2) ? null : ParseDate(reader.GetString(2));
        return new FishingSessionRecord(
            Guid.Parse(reader.GetString(0)),
            ParseDate(reader.GetString(1)),
            ended,
            reader.GetInt32(3));
    }

    public async Task<IReadOnlyList<FishCatch>> GetCatchesAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var catches = new List<FishCatch>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, CaughtAt, Species, Rarity, Mutation, WeightKg,
                   EstimatedValue, RecognitionConfidence
            FROM FishCatches
            WHERE SessionId = $session
            ORDER BY CaughtAt ASC;
            """;
        command.Parameters.AddWithValue("$session", sessionId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            catches.Add(new FishCatch(
                Guid.Parse(reader.GetString(0)),
                ParseDate(reader.GetString(1)),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : (FishRarity)reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetDouble(5),
                reader.IsDBNull(6) ? null : (decimal)reader.GetDouble(6),
                reader.GetDouble(7)));
        }

        return catches;
    }

    public async Task<SessionSummary?> GetSessionSummaryAsync(
        Guid sessionId,
        DateTimeOffset? now = null,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return null;
        }

        var catches = await GetCatchesAsync(sessionId, cancellationToken);
        var endedAt = session.EndedAt ?? now ?? DateTimeOffset.UtcNow;

        return SessionSummaryBuilder.Build(catches, session.StartedAt, endedAt);
    }

    public async Task<IReadOnlyList<FishingSessionRecord>> GetRecentSessionsAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var sessions = new List<FishingSessionRecord>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, StartedAt, EndedAt, TotalCatches
            FROM Sessions
            ORDER BY StartedAt DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            sessions.Add(new FishingSessionRecord(
                Guid.Parse(reader.GetString(0)),
                ParseDate(reader.GetString(1)),
                reader.IsDBNull(2) ? null : ParseDate(reader.GetString(2)),
                reader.GetInt32(3)));
        }

        return sessions;
    }

    private static string FormatDate(DateTimeOffset value) =>
        value.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
