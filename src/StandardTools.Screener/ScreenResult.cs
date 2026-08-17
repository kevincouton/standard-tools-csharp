namespace StandardTools.Screener;

/// <summary>
/// Holds the outcome of a screening run.
/// </summary>
public sealed record ScreenResult
{
    /// <summary>
    /// Tickers that satisfied the criteria.
    /// </summary>
    public required IReadOnlyList<FundamentalData> Matches { get; init; }

    /// <summary>
    /// Tickers that could not be fetched or did not satisfy the criteria.
    /// </summary>
    public required IReadOnlyList<string> Failed { get; init; }
}
