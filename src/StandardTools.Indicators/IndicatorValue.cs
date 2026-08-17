namespace StandardTools.Indicators;

/// <summary>
/// A single date-aligned indicator value. A null <see cref="Value" /> means
/// the indicator is not yet available for that date (warming period).
/// </summary>
public readonly record struct IndicatorValue
{
    public DateOnly Date { get; init; }
    public decimal? Value { get; init; }

    public IndicatorValue(DateOnly date, decimal? value = null)
    {
        Date = date;
        Value = value;
    }
}
