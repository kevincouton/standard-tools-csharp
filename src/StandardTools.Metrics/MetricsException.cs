namespace StandardTools.Metrics;

/// <summary>
/// Base exception for the metrics package.
/// </summary>
public abstract class MetricsException : Exception
{
    protected MetricsException(string message) : base(message) { }
}

/// <summary>
/// Thrown when the supplied price series does not contain enough observations.
/// </summary>
public sealed class InsufficientDataException(string detail)
    : MetricsException($"Insufficient data: {detail}");

/// <summary>
/// Thrown when a price is non-positive, NaN, or infinite.
/// </summary>
public sealed class InvalidPricesException(string detail)
    : MetricsException($"Invalid prices: {detail}");
