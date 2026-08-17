namespace StandardTools.Screener;

/// <summary>
/// Optional bounds on fundamental metrics. A null bound means the filter is disabled. Bounds are inclusive.
/// </summary>
public sealed record ScreenCriteria
{
    public double? PERatioMax { get; init; }
    public double? PERatioMin { get; init; }
    public double? PBRatioMax { get; init; }
    public double? PBRatioMin { get; init; }
    public double? MarketCapMax { get; init; }
    public double? MarketCapMin { get; init; }
    public double? DividendYieldMax { get; init; }
    public double? DividendYieldMin { get; init; }
    public double? EPSGrowthMax { get; init; }
    public double? EPSGrowthMin { get; init; }
    public double? DebtToEquityMax { get; init; }
    public double? DebtToEquityMin { get; init; }
    public double? ROEMax { get; init; }
    public double? ROEMin { get; init; }

    internal bool Apply(FundamentalData data)
    {
        if (!ApplyMax(PERatioMax, data.PERatio)) return false;
        if (!ApplyMin(PERatioMin, data.PERatio)) return false;
        if (!ApplyMax(PBRatioMax, data.PBRatio)) return false;
        if (!ApplyMin(PBRatioMin, data.PBRatio)) return false;
        if (!ApplyMax(MarketCapMax, data.MarketCap)) return false;
        if (!ApplyMin(MarketCapMin, data.MarketCap)) return false;
        if (!ApplyMax(DividendYieldMax, data.DividendYield)) return false;
        if (!ApplyMin(DividendYieldMin, data.DividendYield)) return false;
        if (!ApplyMax(EPSGrowthMax, data.EPSGrowth)) return false;
        if (!ApplyMin(EPSGrowthMin, data.EPSGrowth)) return false;
        if (!ApplyMax(DebtToEquityMax, data.DebtToEquity)) return false;
        if (!ApplyMin(DebtToEquityMin, data.DebtToEquity)) return false;
        if (!ApplyMax(ROEMax, data.ROE)) return false;
        if (!ApplyMin(ROEMin, data.ROE)) return false;
        return true;
    }

    private static bool ApplyMax(double? limit, double value)
    {
        if (!limit.HasValue) return true;
        if (!IsFinite(value)) return false;
        return value <= limit.Value;
    }

    private static bool ApplyMin(double? limit, double value)
    {
        if (!limit.HasValue) return true;
        if (!IsFinite(value)) return false;
        return value >= limit.Value;
    }

    private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
}
