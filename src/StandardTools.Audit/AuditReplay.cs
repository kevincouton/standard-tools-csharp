using StandardTools.Core;

namespace StandardTools.Audit;

/// <summary>
/// Loads a decision record by request ID so it can be inspected or re-executed.
/// </summary>
public sealed class AuditReplay
{
    private readonly IAuditStorage _storage;

    public AuditReplay(IAuditStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <summary>
    /// Loads the decision record for the given request ID.
    /// </summary>
    public Task<DecisionRecord> LoadAsync(string requestID, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestID))
            throw new InvalidCommandException("request_id is required");

        return _storage.GetByRequestIDAsync(requestID, cancellationToken);
    }
}
