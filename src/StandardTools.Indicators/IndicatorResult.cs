namespace StandardTools.Indicators;

/// <summary>
/// The outcome of a single indicator calculation.
/// </summary>
public sealed class IndicatorResult
{
    public required string Name { get; init; }

    public required IReadOnlyDictionary<string, string> Params { get; init; }

    /// <summary>
    /// Date-aligned indicator values. Value is null during the warming period.
    /// </summary>
    public required IReadOnlyList<IndicatorValue> Values { get; init; }

    /// <summary>
    /// Additional named series produced by the indicator, e.g. MACD signal and
    /// histogram, or Bollinger upper and lower bands.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<IndicatorValue>> ExtraSeries { get; init; } =
        new Dictionary<string, IReadOnlyList<IndicatorValue>>();
}
