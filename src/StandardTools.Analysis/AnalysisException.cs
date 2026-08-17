namespace StandardTools.Analysis;

public abstract class AnalysisException : Exception
{
    protected AnalysisException(string message) : base(message) { }
}

public sealed class InsufficientDataException(string detail)
    : AnalysisException($"Insufficient data: {detail}");
