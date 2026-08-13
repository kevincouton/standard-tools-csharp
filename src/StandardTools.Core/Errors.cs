namespace StandardTools.Core;

public abstract class QuantException : Exception
{
    protected QuantException(string message) : base(message) { }
}

public sealed class ProviderNotAvailableException(string provider)
    : QuantException($"Market data provider not available: {provider}");

public sealed class DataQualityException(string detail)
    : QuantException($"Data quality issue: {detail}");

public sealed class InvalidCommandException(string detail)
    : QuantException($"Invalid command: {detail}");
