namespace StandardTools.Audit;

/// <summary>
/// Persists decision records. Implementations must be safe for concurrent use.
/// </summary>
public interface IAuditStorage
{
    Task AppendAsync(DecisionRecord record, CancellationToken cancellationToken = default);
    Task<DecisionRecord> LatestAsync(CancellationToken cancellationToken = default);
    Task<DecisionRecord> GetByRequestIDAsync(string requestID, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DecisionRecord>> AllAsync(CancellationToken cancellationToken = default);
}
