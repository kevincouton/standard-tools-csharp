namespace StandardTools.Screener;

/// <summary>
/// A snapshot of fundamental metrics for a single security.
/// </summary>
public sealed record FundamentalData
{
    public required string Ticker { get; init; }
    public required double MarketCap { get; init; }
    public required double PERatio { get; init; }
    public required double PBRatio { get; init; }
    public required double DividendYield { get; init; }
    public required double EPSGrowth { get; init; }
    public required double DebtToEquity { get; init; }
    public required double ROE { get; init; }
}
