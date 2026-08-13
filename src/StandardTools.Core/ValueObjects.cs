namespace StandardTools.Core;

public enum BarInterval
{
    Daily,
    Weekly,
    Monthly
}

public readonly record struct Ticker
{
    public string Symbol { get; init; }
    public string? Exchange { get; init; }

    public Ticker(string symbol, string? exchange = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        Symbol = symbol;
        Exchange = exchange;
    }
}

public readonly record struct DateRange
{
    public DateOnly Start { get; init; }
    public DateOnly End { get; init; }

    public DateRange(DateOnly start, DateOnly end)
    {
        if (start > end)
            throw new InvalidCommandException("Start date must not be after end date.");

        Start = start;
        End = end;
    }

    public int Days => End.DayNumber - Start.DayNumber + 1;
}

public readonly record struct OHLCV
{
    public Ticker Ticker { get; init; }
    public DateOnly Date { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public long Volume { get; init; }

    public OHLCV(Ticker ticker, DateOnly date, decimal open, decimal high, decimal low, decimal close, long volume)
    {
        if (high < low)
            throw new InvalidCommandException("High must not be less than low.");
        if (open < 0 || high < 0 || low < 0 || close < 0)
            throw new InvalidCommandException("Prices must not be negative.");
        if (volume < 0)
            throw new InvalidCommandException("Volume must not be negative.");

        Ticker = ticker;
        Date = date;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
    }
}

public readonly record struct CacheKey
{
    public string Provider { get; init; }
    public Ticker Ticker { get; init; }
    public BarInterval Interval { get; init; }
    public DateRange Range { get; init; }

    public CacheKey(string provider, Ticker ticker, BarInterval interval, DateRange range)
    {
        Provider = provider;
        Ticker = ticker;
        Interval = interval;
        Range = range;
    }

    public string ToComposite() =>
        $"{Provider}:{Ticker.Symbol}:{Ticker.Exchange ?? ""}:{Interval}:{Range.Start:O}:{Range.End:O}";
}
