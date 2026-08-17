using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StandardTools.Audit;

/// <summary>
/// Records hash-chained decision records to the underlying storage.
/// Writes are serialized within a single process to prevent forked chains.
/// </summary>
public sealed class AuditWriter
{
    private readonly IAuditStorage _storage;
    private readonly Lock _lock = new();

    public AuditWriter(IAuditStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <summary>
    /// Computes hashes and chains the record to the previous one before persisting it.
    /// </summary>
    public async Task WriteAsync(DecisionRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        lock (_lock)
        {
            // Only one write at a time to prevent forked chains.
        }

        var recordedAt = record.RecordedAt == default ? DateTime.UtcNow : record.RecordedAt.ToUniversalTime();

        var latest = await GetLatestOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var prevRecordHash = latest?.RecordHash ?? string.Empty;

        var (inputCanonical, inputHash) = CanonicalizeAndHash(record.Input);
        var (outputCanonical, outputHash) = CanonicalizeAndHash(record.Output);

        var recordHash = HashRecord(record with
        {
            RecordedAt = recordedAt,
            Input = inputCanonical,
            InputHash = inputHash,
            Output = outputCanonical,
            OutputHash = outputHash,
            PrevRecordHash = prevRecordHash,
            RecordHash = string.Empty
        });

        var toStore = record with
        {
            RecordedAt = recordedAt,
            Input = inputCanonical,
            InputHash = inputHash,
            Output = outputCanonical,
            OutputHash = outputHash,
            PrevRecordHash = prevRecordHash,
            RecordHash = recordHash
        };

        await _storage.AppendAsync(toStore, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DecisionRecord?> GetLatestOrDefaultAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _storage.LatestAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (AuditNotFoundException)
        {
            return null;
        }
    }

    private static (JsonElement Canonical, string Hash) CanonicalizeAndHash(object? value)
    {
        var json = value is null ? "null" : JsonSerializer.Serialize(value);
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        using var doc = JsonDocument.Parse(bytes);
        return (doc.RootElement.Clone(), hash);
    }

    /// <summary>
    /// Returns a stable SHA-256 hash of the record excluding its own <see cref="DecisionRecord.RecordHash"/>.
    /// </summary>
    public static string HashRecord(DecisionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var toHash = record with { RecordHash = string.Empty };
        var json = JsonSerializer.Serialize(toHash, AuditJsonOptions.Instance);
        var bytes = Encoding.UTF8.GetBytes(json);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
