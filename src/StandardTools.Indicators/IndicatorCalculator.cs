using System.Globalization;
using StandardTools.Core;

namespace StandardTools.Indicators;

/// <summary>
/// Unified entry point for calculating technical indicators.
/// </summary>
public sealed class IndicatorCalculator
{
    public IndicatorResult Calculate(string name, IReadOnlyList<OHLCV> series, IReadOnlyDictionary<string, string>? parameters = null)
    {
        var parametersDictionary = parameters ?? new Dictionary<string, string>();

        return name.ToLowerInvariant() switch
        {
            "sma" => CalculateSma(series, parametersDictionary),
            "ema" => CalculateEma(series, parametersDictionary),
            "rsi" => CalculateRsi(series, parametersDictionary),
            "macd" => CalculateMacd(series, parametersDictionary),
            "bollinger_bands" => CalculateBollinger(series, parametersDictionary),
            "atr" => CalculateAtr(series, parametersDictionary),
            "obv" => CalculateObv(series, parametersDictionary),
            "vwap" => CalculateVwap(series, parametersDictionary),
            _ => throw new InvalidCommandException($"Unknown indicator: {name}")
        };
    }

    private static IndicatorResult CalculateSma(IReadOnlyList<OHLCV> series, IReadOnlyDictionary<string, string> parameters)
    {
        var (period, merged) = ParseParameterUInt(parameters, "period", 20);
        var values = new List<IndicatorValue>(series.Count);

        if (period == 0 || series.Count < period)
        {
            values.AddRange(series.Select(bar => new IndicatorValue(bar.Date)));
            return new IndicatorResult { Name = "sma", Params = merged, Values = values };
        }

        for (var i = 0; i < series.Count; i++)
        {
            if (i + 1 < period)
            {
                values.Add(new IndicatorValue(series[i].Date));
            }
            else
            {
                var window = series.Skip(i + 1 - period).Take(period).Select(b => b.Close);
                values.Add(new IndicatorValue(series[i].Date, window.Average()));
            }
        }

        return new IndicatorResult { Name = "sma", Params = merged, Values = values };
    }

    private static IndicatorResult CalculateEma(IReadOnlyList<OHLCV> series, IReadOnlyDictionary<string, string> parameters)
    {
        var (period, merged) = ParseParameterUInt(parameters, "period", 20);
        var values = new List<IndicatorValue>(series.Count);

        if (period == 0 || series.Count < period)
        {
            values.AddRange(series.Select(bar => new IndicatorValue(bar.Date)));
            return new IndicatorResult { Name = "ema", Params = merged, Values = values };
        }

        var multiplier = 2m / (period + 1);
        decimal ema = 0;

        for (var i = 0; i < series.Count; i++)
        {
            if (i + 1 < period)
            {
                values.Add(new IndicatorValue(series[i].Date));
            }
            else if (i + 1 == period)
            {
                var window = series.Take(period).Select(b => b.Close);
                ema = window.Average();
                values.Add(new IndicatorValue(series[i].Date, ema));
            }
            else
            {
                ema = (series[i].Close - ema) * multiplier + ema;
                values.Add(new IndicatorValue(series[i].Date, ema));
            }
        }

        return new IndicatorResult { Name = "ema", Params = merged, Values = values };
    }

    private static IndicatorResult CalculateRsi(IReadOnlyList<OHLCV> series, IReadOnlyDictionary<string, string> parameters)
    {
        var (period, merged) = ParseParameterUInt(parameters, "period", 14);
        var values = new List<IndicatorValue>(series.Count);

        if (period == 0 || series.Count < period + 1)
        {
            values.AddRange(series.Select(bar => new IndicatorValue(bar.Date)));
            return new IndicatorResult { Name = "rsi", Params = merged, Values = values };
        }

        var gains = new List<decimal>(period);
        var losses = new List<decimal>(period);

        for (var i = 0; i < period; i++)
        {
            var diff = series[i + 1].Close - series[i].Close;
            gains.Add(diff >= 0 ? diff : 0);
            losses.Add(diff < 0 ? -diff : 0);
        }

        var avgGain = gains.Average();
        var avgLoss = losses.Average();

        for (var i = 0; i < period; i++)
        {
            values.Add(new IndicatorValue(series[i].Date));
        }

        values.Add(new IndicatorValue(series[period].Date, RsiValue(avgGain, avgLoss)));

        for (var i = period; i < series.Count - 1; i++)
        {
            var diff = series[i + 1].Close - series[i].Close;
            var gain = diff >= 0 ? diff : 0;
            var loss = diff < 0 ? -diff : 0;

            avgGain = (avgGain * (period - 1) + gain) / period;
            avgLoss = (avgLoss * (period - 1) + loss) / period;

            values.Add(new IndicatorValue(series[i + 1].Date, RsiValue(avgGain, avgLoss)));
        }

        return new IndicatorResult { Name = "rsi", Params = merged, Values = values };
    }

    private static decimal RsiValue(decimal avgGain, decimal avgLoss)
    {
        if (avgLoss == 0)
            return 100;

        var rs = avgGain / avgLoss;
        return 100 - 100 / (1 + rs);
    }

    private static IndicatorResult CalculateMacd(IReadOnlyList<OHLCV> series, IReadOnlyDictionary<string, string> parameters)
    {
        var (fast, merged) = ParseParameterUInt(parameters, "fast", 12);
        var (slow, merged2) = ParseParameterUInt(merged, "slow", 26);
        var (signal, merged3) = ParseParameterUInt(merged2, "signal", 9);

        var dates = series.Select(b => b.Date).ToArray();
        var closes = series.Select(b => b.Close).ToArray();

        var values = new List<IndicatorValue>(series.Count);
        var signalSeries = new List<IndicatorValue>(series.Count);
        var histogramSeries = new List<IndicatorValue>(series.Count);

        if (fast == 0 || slow == 0 || signal == 0 || series.Count < slow)
        {
            var none = dates.Select(d => new IndicatorValue(d)).ToList();
            return new IndicatorResult
            {
                Name = "macd",
                Params = merged3,
                Values = none,
                ExtraSeries = new Dictionary<string, IReadOnlyList<IndicatorValue>>
                {
                    ["signal"] = none,
                    ["histogram"] = none
                }
            };
        }

        var fastEma = EmaValues(closes, dates, fast);
        var slowEma = EmaValues(closes, dates, slow);

        var macdLine = new IndicatorValue[series.Count];
        for (var i = 0; i < series.Count; i++)
        {
            macdLine[i] = fastEma[i].Value.HasValue && slowEma[i].Value.HasValue
                ? new IndicatorValue(dates[i], fastEma[i].Value!.Value - slowEma[i].Value!.Value)
                : new IndicatorValue(dates[i]);
        }

        var signalEma = EmaOfIndicatorValues(macdLine, signal);

        for (var i = 0; i < series.Count; i++)
        {
            var macd = macdLine[i].Value;
            var sig = signalEma[i].Value;
            decimal? histogram = macd.HasValue && sig.HasValue ? macd.Value - sig.Value : null;

            values.Add(new IndicatorValue(dates[i], macd));
            signalSeries.Add(new IndicatorValue(dates[i], sig));
            histogramSeries.Add(new IndicatorValue(dates[i], histogram));
        }

        return new IndicatorResult
        {
            Name = "macd",
            Params = merged3,
            Values = values,
            ExtraSeries = new Dictionary<string, IReadOnlyList<IndicatorValue>>
            {
                ["signal"] = signalSeries,
                ["histogram"] = histogramSeries
            }
        };
    }

    private static IndicatorResult CalculateBollinger(IReadOnlyList<OHLCV> series, IReadOnlyDictionary<string, string> parameters)
    {
        var (period, merged) = ParseParameterUInt(parameters, "period", 20);
        var (stdDevCount, merged2) = ParseParameterUInt(merged, "std_dev", 2);

        var values = new List<IndicatorValue>(series.Count);
        var upper = new List<IndicatorValue>(series.Count);
        var lower = new List<IndicatorValue>(series.Count);

        if (period == 0 || series.Count < period)
        {
            var none = series.Select(b => new IndicatorValue(b.Date)).ToList();
            return new IndicatorResult
            {
                Name = "bollinger_bands",
                Params = merged2,
                Values = none,
                ExtraSeries = new Dictionary<string, IReadOnlyList<IndicatorValue>>
                {
                    ["upper"] = none,
                    ["lower"] = none
                }
            };
        }

        for (var i = 0; i < series.Count; i++)
        {
            if (i + 1 < period)
            {
                values.Add(new IndicatorValue(series[i].Date));
                upper.Add(new IndicatorValue(series[i].Date));
                lower.Add(new IndicatorValue(series[i].Date));
            }
            else
            {
                var window = series.Skip(i + 1 - period).Take(period).Select(b => b.Close).ToList();
                var middle = window.Average();
                var bandWidth = stdDevCount * SampleStdDev(window);

                values.Add(new IndicatorValue(series[i].Date, middle));
                upper.Add(new IndicatorValue(series[i].Date, middle + bandWidth));
                lower.Add(new IndicatorValue(series[i].Date, middle - bandWidth));
            }
        }

        return new IndicatorResult
        {
            Name = "bollinger_bands",
            Params = merged2,
            Values = values,
            ExtraSeries = new Dictionary<string, IReadOnlyList<IndicatorValue>>
            {
                ["upper"] = upper,
                ["lower"] = lower
            }
        };
    }

    private static IndicatorResult CalculateAtr(IReadOnlyList<OHLCV> series, IReadOnlyDictionary<string, string> parameters)
    {
        var (period, merged) = ParseParameterUInt(parameters, "period", 14);
        var values = new List<IndicatorValue>(series.Count);

        if (period == 0 || series.Count < period + 1)
        {
            values.AddRange(series.Select(bar => new IndicatorValue(bar.Date)));
            return new IndicatorResult { Name = "atr", Params = merged, Values = values };
        }

        var initialTrs = new List<decimal>(period);
        for (var i = 0; i < period; i++)
        {
            initialTrs.Add(TrueRange(series[i + 1], series[i]));
        }

        var atr = initialTrs.Average();

        for (var i = 0; i < period; i++)
        {
            values.Add(new IndicatorValue(series[i].Date));
        }

        values.Add(new IndicatorValue(series[period].Date, atr));

        for (var i = period; i < series.Count - 1; i++)
        {
            var tr = TrueRange(series[i + 1], series[i]);
            atr = (atr * (period - 1) + tr) / period;
            values.Add(new IndicatorValue(series[i + 1].Date, atr));
        }

        return new IndicatorResult { Name = "atr", Params = merged, Values = values };
    }

    private static decimal TrueRange(OHLCV bar, OHLCV previous)
    {
        var highLow = bar.High - bar.Low;
        var highClose = Math.Abs(bar.High - previous.Close);
        var lowClose = Math.Abs(bar.Low - previous.Close);
        return Math.Max(Math.Max(highLow, highClose), lowClose);
    }

    private static IndicatorResult CalculateObv(IReadOnlyList<OHLCV> series, IReadOnlyDictionary<string, string> parameters)
    {
        var values = new List<IndicatorValue>(series.Count);

        if (series.Count == 0)
        {
            return new IndicatorResult { Name = "obv", Params = parameters, Values = values };
        }

        var obv = (decimal)series[0].Volume;
        values.Add(new IndicatorValue(series[0].Date, obv));

        for (var i = 0; i < series.Count - 1; i++)
        {
            var currentClose = series[i + 1].Close;
            var previousClose = series[i].Close;
            var volume = (decimal)series[i + 1].Volume;

            if (currentClose > previousClose)
                obv += volume;
            else if (currentClose < previousClose)
                obv -= volume;

            values.Add(new IndicatorValue(series[i + 1].Date, obv));
        }

        return new IndicatorResult { Name = "obv", Params = parameters, Values = values };
    }

    private static IndicatorResult CalculateVwap(IReadOnlyList<OHLCV> series, IReadOnlyDictionary<string, string> parameters)
    {
        var values = new List<IndicatorValue>(series.Count);
        decimal cumulativeTpVolume = 0;
        decimal cumulativeVolume = 0;

        foreach (var bar in series)
        {
            var typicalPrice = (bar.High + bar.Low + bar.Close) / 3;
            var volume = (decimal)bar.Volume;

            cumulativeTpVolume += typicalPrice * volume;
            cumulativeVolume += volume;

            decimal? vwap = cumulativeVolume == 0 ? null : cumulativeTpVolume / cumulativeVolume;
            values.Add(new IndicatorValue(bar.Date, vwap));
        }

        return new IndicatorResult { Name = "vwap", Params = parameters, Values = values };
    }

    private static IReadOnlyList<IndicatorValue> EmaValues(IReadOnlyList<decimal> closes, IReadOnlyList<DateOnly> dates, int period)
    {
        var values = new List<IndicatorValue>(closes.Count);

        if (period == 0 || closes.Count < period)
        {
            values.AddRange(dates.Select(d => new IndicatorValue(d)));
            return values;
        }

        var multiplier = 2m / (period + 1);
        decimal ema = 0;

        for (var i = 0; i < closes.Count; i++)
        {
            if (i + 1 < period)
            {
                values.Add(new IndicatorValue(dates[i]));
            }
            else if (i + 1 == period)
            {
                ema = closes.Take(period).Average();
                values.Add(new IndicatorValue(dates[i], ema));
            }
            else
            {
                ema = (closes[i] - ema) * multiplier + ema;
                values.Add(new IndicatorValue(dates[i], ema));
            }
        }

        return values;
    }

    private static IReadOnlyList<IndicatorValue> EmaOfIndicatorValues(IReadOnlyList<IndicatorValue> series, int period)
    {
        var result = new List<IndicatorValue>(series.Count);

        if (period == 0)
        {
            result.AddRange(series.Select(v => new IndicatorValue(v.Date)));
            return result;
        }

        var multiplier = 2m / (period + 1);
        decimal? ema = null;
        var seen = 0;
        var seedValues = new List<decimal>(period);

        foreach (var iv in series)
        {
            if (iv.Value.HasValue && !ema.HasValue)
            {
                seen++;
                seedValues.Add(iv.Value.Value);

                if (seen == period)
                {
                    var seed = seedValues.Average();
                    ema = seed;
                    result.Add(new IndicatorValue(iv.Date, seed));
                }
                else
                {
                    result.Add(new IndicatorValue(iv.Date));
                }
            }
            else if (iv.Value.HasValue && ema.HasValue)
            {
                var next = (iv.Value.Value - ema.Value) * multiplier + ema.Value;
                ema = next;
                result.Add(new IndicatorValue(iv.Date, next));
            }
            else
            {
                result.Add(new IndicatorValue(iv.Date));
            }
        }

        return result;
    }

    private static decimal SampleStdDev(IReadOnlyList<decimal> values)
    {
        if (values.Count < 2)
            return 0;

        var sum = values.Sum();
        var mean = sum / values.Count;
        var variance = values.Select(v => (double)((v - mean) * (v - mean))).Sum() / (values.Count - 1);
        return (decimal)Math.Sqrt(variance);
    }

    private static (int value, IReadOnlyDictionary<string, string> merged) ParseParameterUInt(
        IReadOnlyDictionary<string, string> parameters,
        string key,
        int defaultValue)
    {
        var merged = new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase)
        {
            [key] = parameters.TryGetValue(key, out var raw) ? raw : defaultValue.ToString(CultureInfo.InvariantCulture)
        };

        if (!int.TryParse(merged[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 0)
        {
            throw new InvalidCommandException($"Invalid value for {key}: {merged[key]}");
        }

        return (value, merged);
    }
}
