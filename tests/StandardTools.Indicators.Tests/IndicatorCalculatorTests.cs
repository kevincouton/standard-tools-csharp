using System.Globalization;
using StandardTools.Core;
using Xunit;

namespace StandardTools.Indicators.Tests;

public class IndicatorCalculatorTests
{
    private static IReadOnlyList<OHLCV> NewSeriesFromCloses(params decimal[] closes)
    {
        var start = new DateOnly(2024, 1, 1);
        var series = new List<OHLCV>(closes.Length);

        for (var i = 0; i < closes.Length; i++)
        {
            var close = closes[i];
            var open = close - 0.1m;
            var high = close + 0.2m;
            var low = close - 0.2m;
            series.Add(new OHLCV(
                new Ticker("AAPL"),
                start.AddDays(i),
                open,
                high,
                low,
                close,
                1000 + i * 100));
        }

        return series;
    }

    private static decimal ValueAt(IndicatorResult result, int index)
    {
        Assert.True(result.Values[index].Value.HasValue, $"expected value at index {index}");
        return result.Values[index].Value!.Value;
    }

    [Fact]
    public void UnknownIndicator_ThrowsInvalidCommandException()
    {
        var calc = new IndicatorCalculator();
        var series = NewSeriesFromCloses(1, 2, 3, 4, 5);
        Assert.Throws<InvalidCommandException>(() => calc.Calculate("unknown", series));
    }

    [Fact]
    public void InvalidParameter_ThrowsInvalidCommandException()
    {
        var calc = new IndicatorCalculator();
        var series = NewSeriesFromCloses(1, 2, 3, 4, 5);
        Assert.Throws<InvalidCommandException>(() => calc.Calculate("sma", series, new Dictionary<string, string> { ["period"] = "not-a-number" }));
    }

    [Fact]
    public void Sma_KnownValues()
    {
        var calc = new IndicatorCalculator();
        var series = NewSeriesFromCloses(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        var result = calc.Calculate("sma", series, new Dictionary<string, string> { ["period"] = "3" });

        Assert.Equal("sma", result.Name);
        Assert.Null(result.Values[0].Value);
        Assert.Null(result.Values[1].Value);

        var expected = new[] { 0m, 0m, 2m, 3m, 4m, 5m, 6m, 7m, 8m, 9m };
        for (var i = 2; i < series.Count; i++)
        {
            Assert.Equal(expected[i], ValueAt(result, i));
        }
    }

    [Fact]
    public void Sma_DefaultPeriod()
    {
        var calc = new IndicatorCalculator();
        var series = NewSeriesFromCloses(Enumerable.Range(1, 25).Select(i => (decimal)i).ToArray());
        var result = calc.Calculate("sma", series);

        Assert.Equal("20", result.Params["period"]);
        Assert.True(result.Values[19].Value.HasValue);
        Assert.Equal(10.5m, result.Values[19].Value!.Value);
    }

    [Fact]
    public void Ema_Sanity()
    {
        var calc = new IndicatorCalculator();
        var series = NewSeriesFromCloses(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        var result = calc.Calculate("ema", series, new Dictionary<string, string> { ["period"] = "3" });

        Assert.Equal("ema", result.Name);
        Assert.Null(result.Values[0].Value);
        Assert.Null(result.Values[1].Value);

        var prev = ValueAt(result, 2);
        for (var i = 3; i < series.Count; i++)
        {
            var actual = ValueAt(result, i);
            Assert.True(actual >= prev, "EMA should be non-decreasing for strictly increasing closes");
            Assert.True(actual >= 1 && actual <= 10, "EMA should stay within min/max close");
            prev = actual;
        }
    }

    [Fact]
    public void Rsi_AllUp_Is100()
    {
        var calc = new IndicatorCalculator();
        var series = NewSeriesFromCloses(Enumerable.Range(1, 30).Select(i => (decimal)i).ToArray());
        var result = calc.Calculate("rsi", series, new Dictionary<string, string> { ["period"] = "14" });

        Assert.Equal("rsi", result.Name);
        for (var i = 0; i < 14; i++)
        {
            Assert.Null(result.Values[i].Value);
        }

        Assert.Equal(100m, ValueAt(result, series.Count - 1));
    }

    [Fact]
    public void Rsi_AllDown_Is0()
    {
        var calc = new IndicatorCalculator();
        var series = NewSeriesFromCloses(Enumerable.Range(1, 30).Select(i => (decimal)(31 - i)).ToArray());
        var result = calc.Calculate("rsi", series, new Dictionary<string, string> { ["period"] = "14" });

        Assert.Equal(0m, ValueAt(result, series.Count - 1));
    }

    [Fact]
    public void Rsi_Range()
    {
        var calc = new IndicatorCalculator();
        var series = NewSeriesFromCloses(Enumerable.Range(0, 50).Select(i => (decimal)(i % 10 + 1)).ToArray());
        var result = calc.Calculate("rsi", series);

        foreach (var v in result.Values)
        {
            if (v.Value.HasValue)
            {
                Assert.True(v.Value.Value >= 0 && v.Value.Value <= 100, $"RSI {v.Value.Value} out of range");
            }
        }
    }

    [Fact]
    public void Macd_ExtraSeries()
    {
        var calc = new IndicatorCalculator();
        var series = NewSeriesFromCloses(Enumerable.Range(1, 60).Select(i => (decimal)i).ToArray());
        var result = calc.Calculate("macd", series);

        Assert.Equal("macd", result.Name);
        Assert.Equal(series.Count, result.Values.Count);
        Assert.Contains("signal", result.ExtraSeries);
        Assert.Contains("histogram", result.ExtraSeries);
        Assert.Equal(series.Count, result.ExtraSeries["signal"].Count);
        Assert.Equal(series.Count, result.ExtraSeries["histogram"].Count);
    }

    [Fact]
    public void BollingerBands_UpperAndLower()
    {
        var calc = new IndicatorCalculator();
        var series = NewSeriesFromCloses(Enumerable.Range(1, 30).Select(i => (decimal)i).ToArray());
        var result = calc.Calculate("bollinger_bands", series);

        Assert.Equal("bollinger_bands", result.Name);
        Assert.Contains("upper", result.ExtraSeries);
        Assert.Contains("lower", result.ExtraSeries);

        for (var i = 19; i < series.Count; i++)
        {
            var middle = ValueAt(result, i);
            var upper = result.ExtraSeries["upper"][i].Value;
            var lower = result.ExtraSeries["lower"][i].Value;
            Assert.True(upper.HasValue && lower.HasValue);
            Assert.True(upper.Value >= middle, "upper band should be >= middle band");
            Assert.True(lower.Value <= middle, "lower band should be <= middle band");
        }
    }

    [Fact]
    public void Atr_Sanity()
    {
        var calc = new IndicatorCalculator();
        var series = NewSeriesFromCloses(10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25);
        var result = calc.Calculate("atr", series, new Dictionary<string, string> { ["period"] = "5" });

        Assert.Equal("atr", result.Name);
        for (var i = 0; i < 5; i++)
        {
            Assert.Null(result.Values[i].Value);
        }

        for (var i = 5; i < series.Count; i++)
        {
            Assert.True(ValueAt(result, i) > 0, "ATR should be positive");
        }
    }

    [Fact]
    public void Obv_Trend()
    {
        var calc = new IndicatorCalculator();
        var series = NewSeriesFromCloses(10, 11, 10, 12, 11);
        var result = calc.Calculate("obv", series);

        Assert.Equal("obv", result.Name);
        Assert.All(result.Values, v => Assert.True(v.Value.HasValue));
        Assert.Equal(series[0].Volume, ValueAt(result, 0));
    }

    [Fact]
    public void Vwap_Sanity()
    {
        var calc = new IndicatorCalculator();
        var series = NewSeriesFromCloses(10, 11, 12, 13, 14);
        var result = calc.Calculate("vwap", series);

        Assert.Equal("vwap", result.Name);
        Assert.All(result.Values, v =>
        {
            Assert.True(v.Value.HasValue);
            Assert.True(v.Value.Value > 0, "VWAP should be positive");
        });
    }
}
