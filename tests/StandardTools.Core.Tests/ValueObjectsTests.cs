using StandardTools.Core;
using Xunit;

namespace StandardTools.Core.Tests;

public class ValueObjectsTests
{
    [Fact]
    public void Ticker_RejectsBlankSymbol()
    {
        Assert.Throws<ArgumentException>(() => new Ticker(" "));
    }

    [Fact]
    public void DateRange_RejectsStartAfterEnd()
    {
        var start = new DateOnly(2024, 1, 10);
        var end = new DateOnly(2024, 1, 1);

        Assert.Throws<InvalidCommandException>(() => new DateRange(start, end));
    }

    [Fact]
    public void OHLCV_RejectsHighLessThanLow()
    {
        var ticker = new Ticker("AAPL");
        var date = new DateOnly(2024, 1, 2);

        Assert.Throws<InvalidCommandException>(() =>
            new OHLCV(ticker, date, 100m, 99m, 100m, 100m, 1_000_000));
    }

    [Fact]
    public void OHLCV_RejectsNegativePrice()
    {
        var ticker = new Ticker("AAPL");
        var date = new DateOnly(2024, 1, 2);

        Assert.Throws<InvalidCommandException>(() =>
            new OHLCV(ticker, date, -1m, 100m, 99m, 100m, 1_000_000));
    }

    [Fact]
    public void CacheKey_ToComposite_IncludesAllParts()
    {
        var key = new CacheKey(
            "yfinance",
            new Ticker("AAPL", "NASDAQ"),
            BarInterval.Daily,
            new DateRange(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 5)));

        Assert.Equal("yfinance:AAPL:NASDAQ:Daily:2024-01-01:2024-01-05", key.ToComposite());
    }
}
