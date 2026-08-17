using StandardTools.Core;

namespace StandardTools.Audit;

/// <summary>
/// Thrown when an audit record cannot be found.
/// </summary>
public sealed class AuditNotFoundException(string detail)
    : QuantException($"Audit record not found: {detail}");
