using System.Text.Json.Serialization;

namespace StandardTools.Screener;

/// <summary>
/// Optional bounds on fundamental metrics. A null bound means the filter is disabled. Bounds are inclusive.
/// </summary>
public sealed record ScreenCriteria
{
    [JsonPropertyName("pe_ratio_max")]
    public double? PERatioMax { get; init; }

    [JsonPropertyName("pe_ratio_min")]
    public double? PERatioMin { get; init; }

    [JsonPropertyName("pb_ratio_max")]
    public double? PBRatioMax { get; init; }

    [JsonPropertyName("pb_ratio_min")]
    public double? PBRatioMin { get; init; }

    [JsonPropertyName("market_cap_max")]
    public double? MarketCapMax { get; init; }

    [JsonPropertyName("market_cap_min")]
    public double? MarketCapMin { get; init; }

    [JsonPropertyName("dividend_yield_max")]
    public double? DividendYieldMax { get; init; }

    [JsonPropertyName("dividend_yield_min")]
    public double? DividendYieldMin { get; init; }

    [JsonPropertyName("eps_growth_max")]
    public double? EPSGrowthMax { get; init; }

    [JsonPropertyName("eps_growth_min")]
    public double? EPSGrowthMin { get; init; }

    [JsonPropertyName("debt_to_equity_max")]
    public double? DebtToEquityMax { get; init; }

    [JsonPropertyName("debt_to_equity_min")]
    public double? DebtToEquityMin { get; init; }

    [JsonPropertyName("roe_max")]
    public double? ROEMax { get; init; }

    [JsonPropertyName("roe_min")]
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
