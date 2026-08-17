using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StandardTools.Audit;

/// <summary>
/// Checks the integrity of the stored audit chain.
/// </summary>
public sealed class AuditVerifier
{
    private readonly IAuditStorage _storage;

    public AuditVerifier(IAuditStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <summary>
    /// Verifies every link in the audit chain by recomputing the input, output, and record hashes
    /// and comparing them to the stored values. Also checks that each record's previous hash
    /// matches the previous record's record hash.
    /// </summary>
    public async Task VerifyChainAsync(CancellationToken cancellationToken = default)
    {
        var records = await _storage.AllAsync(cancellationToken).ConfigureAwait(false);

        for (var i = 0; i < records.Count; i++)
        {
            var r = records[i];

            var inputHash = HashAny(r.Input);
            if (inputHash != r.InputHash)
                throw new AuditIntegrityException($"record {i} ({r.RequestID}): input hash mismatch");

            var outputHash = HashAny(r.Output);
            if (outputHash != r.OutputHash)
                throw new AuditIntegrityException($"record {i} ({r.RequestID}): output hash mismatch");

            var recordHash = AuditWriter.HashRecord(r);
            if (recordHash != r.RecordHash)
                throw new AuditIntegrityException($"record {i} ({r.RequestID}): record hash mismatch");

            if (i > 0 && r.PrevRecordHash != records[i - 1].RecordHash)
                throw new AuditIntegrityException($"record {i} ({r.RequestID}): previous record hash mismatch");
        }
    }

    private static string HashAny(object? value)
    {
        var json = value is null ? "null" : JsonSerializer.Serialize(value);
        var bytes = Encoding.UTF8.GetBytes(json);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
