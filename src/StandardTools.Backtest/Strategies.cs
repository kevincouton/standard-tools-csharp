using StandardTools.Core;
using StandardTools.Indicators;

namespace StandardTools.Backtest;

/// <summary>
/// Generates trading signals for a price series.
/// </summary>
public interface IStrategy
{
    string Name { get; }
    IReadOnlyList<SignalResult> Signals(IReadOnlyList<OHLCV> series, IReadOnlyDictionary<string, string> parameters);
}

internal static class StrategyFactory
{
    public static IStrategy Create(string name) => name.ToLowerInvariant() switch
    {
        "buy_and_hold" => new BuyAndHoldStrategy(),
        "sma_crossover" => new SmaCrossoverStrategy(),
        "rsi_threshold" => new RsiThresholdStrategy(),
        "bollinger_bands_reversion" => new BollingerReversionStrategy(),
        "macd_crossover" => new MacdCrossoverStrategy(),
        _ => throw new InvalidCommandException($"Unknown strategy: {name}")
    };
}

internal sealed class BuyAndHoldStrategy : IStrategy
{
    public string Name => "buy_and_hold";

    public IReadOnlyList<SignalResult> Signals(IReadOnlyList<OHLCV> series, IReadOnlyDictionary<string, string> parameters) =>
        series.Select((bar, i) => new SignalResult(bar.Date, i == 0 ? SignalType.Buy : SignalType.Hold)).ToArray();
}

internal sealed class SmaCrossoverStrategy : IStrategy
{
    public string Name => "sma_crossover";

    public IReadOnlyList<SignalResult> Signals(IReadOnlyList<OHLCV> series, IReadOnlyDictionary<string, string> parameters)
    {
        var calc = new IndicatorCalculator();
        var fastParams = new Dictionary<string, string>(parameters) { ["period"] = parameters.GetValueOrDefault("fast", "10") };
        var slowParams = new Dictionary<string, string>(parameters) { ["period"] = parameters.GetValueOrDefault("slow", "30") };
        var fast = calc.Calculate("sma", series, fastParams).Values;
        var slow = calc.Calculate("sma", series, slowParams).Values;
        return StrategyHelpers.CrossoverSignals(series, fast, slow);
    }
}

internal sealed class RsiThresholdStrategy : IStrategy
{
    public string Name => "rsi_threshold";

    public IReadOnlyList<SignalResult> Signals(IReadOnlyList<OHLCV> series, IReadOnlyDictionary<string, string> parameters)
    {
        var calc = new IndicatorCalculator();
        var oversold = decimal.Parse(parameters.GetValueOrDefault("oversold", "30"));
        var overbought = decimal.Parse(parameters.GetValueOrDefault("overbought", "70"));
        var rsi = calc.Calculate("rsi", series, parameters).Values;

        return series.Select((bar, i) =>
        {
            if (i == 0 || !rsi[i].Value.HasValue)
                return new SignalResult(bar.Date, SignalType.Hold);

            var v = rsi[i].Value!.Value;
            if (v < oversold) return new SignalResult(bar.Date, SignalType.Buy);
            if (v > overbought) return new SignalResult(bar.Date, SignalType.Sell);
            return new SignalResult(bar.Date, SignalType.Hold);
        }).ToArray();
    }
}

internal sealed class BollingerReversionStrategy : IStrategy
{
    public string Name => "bollinger_bands_reversion";

    public IReadOnlyList<SignalResult> Signals(IReadOnlyList<OHLCV> series, IReadOnlyDictionary<string, string> parameters)
    {
        var calc = new IndicatorCalculator();
        var bb = calc.Calculate("bollinger_bands", series, parameters);
        var upper = bb.ExtraSeries["upper"];
        var lower = bb.ExtraSeries["lower"];

        return series.Select((bar, i) =>
        {
            if (i >= upper.Count || i >= lower.Count)
                return new SignalResult(bar.Date, SignalType.Hold);

            var u = upper[i].Value;
            var l = lower[i].Value;
            if (u.HasValue && bar.Close > u.Value) return new SignalResult(bar.Date, SignalType.Sell);
            if (l.HasValue && bar.Close < l.Value) return new SignalResult(bar.Date, SignalType.Buy);
            return new SignalResult(bar.Date, SignalType.Hold);
        }).ToArray();
    }
}

internal sealed class MacdCrossoverStrategy : IStrategy
{
    public string Name => "macd_crossover";

    public IReadOnlyList<SignalResult> Signals(IReadOnlyList<OHLCV> series, IReadOnlyDictionary<string, string> parameters)
    {
        var calc = new IndicatorCalculator();
        var macd = calc.Calculate("macd", series, parameters);
        var signal = macd.ExtraSeries["signal"];
        return StrategyHelpers.CrossoverSignals(series, macd.Values, signal);
    }
}

internal static class StrategyHelpers
{
    public static IReadOnlyList<SignalResult> CrossoverSignals(
        IReadOnlyList<OHLCV> series,
        IReadOnlyList<IndicatorValue> fast,
        IReadOnlyList<IndicatorValue> slow)
    {
        var signals = new List<SignalResult>(series.Count);
        for (var i = 0; i < series.Count; i++)
        {
            var signal = SignalType.Hold;
            if (i < fast.Count && i < slow.Count && fast[i].Value.HasValue && slow[i].Value.HasValue)
            {
                var f = fast[i].Value!.Value;
                var s = slow[i].Value!.Value;
                if (i == 0)
                {
                    signal = f > s ? SignalType.Buy : f < s ? SignalType.Sell : SignalType.Hold;
                }
                else if (i - 1 < fast.Count && i - 1 < slow.Count && fast[i - 1].Value.HasValue && slow[i - 1].Value.HasValue)
                {
                    var pf = fast[i - 1].Value!.Value;
                    var ps = slow[i - 1].Value!.Value;
                    if (f > s && !(pf > ps)) signal = SignalType.Buy;
                    else if (f < s && !(pf < ps)) signal = SignalType.Sell;
                }
            }
            signals.Add(new SignalResult(series[i].Date, signal));
        }
        return signals;
    }

    public static string GetValueOrDefault(this IReadOnlyDictionary<string, string> parameters, string key, string defaultValue) =>
        parameters.TryGetValue(key, out var value) ? value : defaultValue;
}
