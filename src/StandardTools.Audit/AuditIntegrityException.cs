using StandardTools.Core;

namespace StandardTools.Audit;

/// <summary>
/// Thrown when the audit chain fails integrity verification.
/// </summary>
public sealed class AuditIntegrityException(string detail)
    : QuantException($"Audit integrity failure: {detail}");
