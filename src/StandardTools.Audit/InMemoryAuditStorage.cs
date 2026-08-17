using StandardTools.Core;

namespace StandardTools.Audit;

/// <summary>
/// In-memory audit storage for unit tests and lightweight deployments.
/// </summary>
public sealed class InMemoryAuditStorage : IAuditStorage
{
    private readonly List<DecisionRecord> _records = new();
    private readonly Lock _lock = new();

    public Task AppendAsync(DecisionRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_lock)
        {
            _records.Add(record);
        }
        return Task.CompletedTask;
    }

    public Task<DecisionRecord> LatestAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_records.Count == 0)
                throw new AuditNotFoundException("no audit records found");
            return Task.FromResult(_records[_records.Count - 1]);
        }
    }

    public Task<DecisionRecord> GetByRequestIDAsync(string requestID, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestID);
        lock (_lock)
        {
            for (var i = _records.Count - 1; i >= 0; i--)
            {
                if (_records[i].RequestID == requestID)
                    return Task.FromResult(_records[i]);
            }
            throw new AuditNotFoundException($"request {requestID} not found");
        }
    }

    public Task<IReadOnlyList<DecisionRecord>> AllAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<DecisionRecord>>(_records.ToArray());
        }
    }
}
