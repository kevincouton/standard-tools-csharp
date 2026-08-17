using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace StandardTools.Audit;

/// <summary>
/// SQLite-backed persistent storage for hash-chained decision records.
/// Writes are serialized with a SQLite-exclusive transaction so the chain
/// cannot fork under concurrent callers.
/// </summary>
public sealed class SqliteAuditStorage : IAuditStorage, IAsyncDisposable
{
    private readonly string? _connectionString;
    private readonly SqliteConnection? _persistentConnection;
    private readonly Lock _initLock = new();
    private bool _initialized;

    public SqliteAuditStorage(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    private SqliteAuditStorage(SqliteConnection persistentConnection)
    {
        _persistentConnection = persistentConnection ?? throw new ArgumentNullException(nameof(persistentConnection));
    }

    /// <summary>
    /// Creates an in-memory SQLite storage. Useful for tests.
    /// A single connection is held open so the schema and data survive across calls.
    /// </summary>
    public static SqliteAuditStorage CreateInMemory()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return new SqliteAuditStorage(connection);
    }

    /// <summary>
    /// Opens a connection for the current operation. The caller owns disposal
    /// unless the storage was created with <see cref="CreateInMemory"/>.
    /// </summary>
    private SqliteConnection OpenConnection()
    {
        if (_persistentConnection is not null)
            return _persistentConnection;

        var connection = new SqliteConnection(_connectionString!);
        connection.Open();
        return connection;
    }

    private void EnsureSchema()
    {
        if (_initialized)
            return;

        lock (_initLock)
        {
            if (_initialized)
                return;

            var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE IF NOT EXISTS decision_records (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    request_id TEXT NOT NULL UNIQUE,
                    recorded_at TEXT NOT NULL,
                    tool_name TEXT NOT NULL,
                    status TEXT NOT NULL,
                    prev_record_hash TEXT NOT NULL,
                    record_hash TEXT NOT NULL UNIQUE,
                    record_json TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_decision_records_hash ON decision_records(record_hash);
                CREATE INDEX IF NOT EXISTS idx_decision_records_request_id ON decision_records(request_id);
                """;
            command.ExecuteNonQuery();
            _initialized = true;
        }
    }

    public async Task AppendAsync(DecisionRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        EnsureSchema();

        var connection = OpenConnection();

        using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = (SqliteTransaction)transaction;
                command.CommandText =
                    """
                    INSERT INTO decision_records (request_id, recorded_at, tool_name, status, prev_record_hash, record_hash, record_json)
                    VALUES ($request_id, $recorded_at, $tool_name, $status, $prev_record_hash, $record_hash, $record_json);
                    """;
                command.Parameters.AddWithValue("$request_id", record.RequestID);
                command.Parameters.AddWithValue("$recorded_at", record.RecordedAt.ToString("O"));
                command.Parameters.AddWithValue("$tool_name", record.ToolName);
                command.Parameters.AddWithValue("$status", record.Status);
                command.Parameters.AddWithValue("$prev_record_hash", record.PrevRecordHash);
                command.Parameters.AddWithValue("$record_hash", record.RecordHash);
                command.Parameters.AddWithValue("$record_json", JsonSerializer.Serialize(record, AuditJsonOptions.Instance));
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<DecisionRecord> LatestAsync(CancellationToken cancellationToken = default)
    {
        EnsureSchema();

        var connection = OpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT record_json FROM decision_records
            ORDER BY id DESC
            LIMIT 1;
            """;

        var json = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        if (json is null)
            throw new AuditNotFoundException("no audit records found");

        return Deserialize(json);
    }

    public async Task<DecisionRecord> GetByRequestIDAsync(string requestID, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestID);
        EnsureSchema();

        var connection = OpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT record_json FROM decision_records
            WHERE request_id = $request_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$request_id", requestID);

        var json = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        if (json is null)
            throw new AuditNotFoundException($"request {requestID} not found");

        return Deserialize(json);
    }

    public async Task<IReadOnlyList<DecisionRecord>> AllAsync(CancellationToken cancellationToken = default)
    {
        EnsureSchema();

        var connection = OpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT record_json FROM decision_records
            ORDER BY id ASC;
            """;

        var records = new List<DecisionRecord>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            records.Add(Deserialize(reader.GetString(0)));
        }

        return records;
    }

    private static DecisionRecord Deserialize(string json) =>
        JsonSerializer.Deserialize<DecisionRecord>(json, AuditJsonOptions.Instance)
        ?? throw new AuditIntegrityException("stored audit record deserialized to null");

    public async ValueTask DisposeAsync()
    {
        if (_persistentConnection is not null)
        {
            await _persistentConnection.DisposeAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }
}
