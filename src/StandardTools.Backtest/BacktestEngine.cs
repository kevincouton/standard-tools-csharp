using StandardTools.Core;

namespace StandardTools.Backtest;

/// <summary>
/// Runs a single-asset vectorized backtest using next-bar-open execution.
/// </summary>
public sealed class BacktestEngine
{
    private readonly IStrategy _strategy;
    private readonly BacktestConfig _config;

    public BacktestEngine(string strategyName, BacktestConfig config)
    {
        _strategy = StrategyFactory.Create(strategyName);
        _config = config;
    }

    public BacktestEngine(IStrategy strategy, BacktestConfig config)
    {
        _strategy = strategy;
        _config = config;
    }

    public BacktestResult Run(IReadOnlyList<OHLCV> series, IReadOnlyDictionary<string, string>? parameters = null)
    {
        if (series.Count == 0)
            throw new InvalidCommandException("Backtest requires a non-empty price series");

        var parametersDictionary = parameters ?? new Dictionary<string, string>();
        var signals = _strategy.Signals(series, parametersDictionary);
        if (signals.Count != series.Count)
            throw new DataQualityException("Strategy signal count does not match series length");

        var commission = (decimal)_config.CommissionRate;
        var cash = _config.InitialCapital;
        Position? position = null;
        var trades = new List<Trade>();
        var equityCurve = new List<EquityPoint>(series.Count);

        for (var i = 0; i < series.Count; i++)
        {
            var bar = series[i];
            if (i > 0)
            {
                switch (signals[i - 1].Signal)
                {
                    case SignalType.Buy:
                        if (position is null || position.Side != TradeSide.Long)
                        {
                            if (position is not null)
                            {
                                var (t, c) = ClosePosition(position, bar.Open, commission, bar.Date, cash);
                                cash = c;
                                trades.Add(t);
                                position = null;
                            }
                            var (p, c2) = OpenLong(bar.Open, cash, commission, bar.Date);
                            cash = c2;
                            position = p;
                        }
                        break;
                    case SignalType.Sell:
                        if (position is null || position.Side != TradeSide.Short)
                        {
                            if (position is not null)
                            {
                                var (t, c) = ClosePosition(position, bar.Open, commission, bar.Date, cash);
                                cash = c;
                                trades.Add(t);
                                position = null;
                            }
                            var (p, c2) = OpenShort(bar.Open, cash, commission, bar.Date);
                            cash = c2;
                            position = p;
                        }
                        break;
                }
            }

            var equity = cash;
            if (position is not null)
                equity += PositionMarketValue(position, bar.Close);
            equityCurve.Add(new EquityPoint(bar.Date, equity));
        }

        if (position is not null)
        {
            var last = series[^1];
            var (t, c) = ClosePosition(position, last.Close, commission, last.Date, cash);
            cash = c;
            trades.Add(t);
            if (equityCurve.Count > 0)
                equityCurve[^1] = new EquityPoint(last.Date, cash);
        }

        var metrics = ComputeMetrics(equityCurve, _config);
        var winRate = trades.Count == 0 ? 0.0 : (double)trades.Count(t => t.PnL > 0) / trades.Count;
        var finalEquity = equityCurve.Count > 0 ? equityCurve[^1].Equity : _config.InitialCapital;

        return new BacktestResult(
            finalEquity,
            metrics.TotalReturn,
            equityCurve,
            trades,
            new BacktestMetrics(metrics.MaxDrawdown, metrics.Sharpe, winRate, trades.Count));
    }

    private static (Position Position, decimal Cash) OpenLong(decimal price, decimal cash, decimal commission, DateOnly date)
    {
        var quantity = price == 0 ? 0 : cash / (price * (1 + commission));
        var comm = quantity * price * commission;
        var position = new Position(TradeSide.Long, price, quantity, date);
        return (position, cash - quantity * price - comm);
    }

    private static (Position Position, decimal Cash) OpenShort(decimal price, decimal cash, decimal commission, DateOnly date)
    {
        var quantity = price == 0 ? 0 : cash / (price * (1 + commission));
        var comm = quantity * price * commission;
        var position = new Position(TradeSide.Short, price, quantity, date);
        return (position, cash - comm);
    }

    private static (Trade Trade, decimal Cash) ClosePosition(Position position, decimal price, decimal commission, DateOnly date, decimal cash)
    {
        var entryComm = position.Quantity * position.EntryPrice * commission;
        var exitComm = position.Quantity * price * commission;
        decimal pnl, newCash;

        if (position.Side == TradeSide.Long)
        {
            pnl = position.Quantity * (price - position.EntryPrice) - entryComm - exitComm;
            newCash = cash + position.Quantity * price - exitComm;
        }
        else
        {
            pnl = position.Quantity * (position.EntryPrice - price) - entryComm - exitComm;
            newCash = cash + position.Quantity * position.EntryPrice - position.Quantity * price - exitComm;
        }

        var trade = new Trade(position.EntryDate, date, position.EntryPrice, price, position.Quantity, position.Side, pnl);
        return (trade, newCash);
    }

    private static decimal PositionMarketValue(Position position, decimal price) =>
        position.Side == TradeSide.Long ? position.Quantity * price : -position.Quantity * price;

    private static ComputedMetrics ComputeMetrics(IReadOnlyList<EquityPoint> curve, BacktestConfig config)
    {
        if (curve.Count == 0)
            return new ComputedMetrics(0, 0, null);

        var initial = curve[0].Equity;
        var final = curve[^1].Equity;
        var totalReturn = initial == 0 ? 0.0 : (double)(final / initial - 1);

        var returns = new double[curve.Count - 1];
        for (var i = 1; i < curve.Count; i++)
        {
            var prev = curve[i - 1].Equity;
            returns[i - 1] = prev == 0 ? 0.0 : (double)(curve[i].Equity / prev - 1);
        }

        var maxDD = ComputeMaxDrawdown(curve);
        var sharpe = ComputeSharpe(returns, config.RiskFreeRate, config.PeriodsPerYear);
        return new ComputedMetrics(totalReturn, maxDD, sharpe);
    }

    private static double ComputeMaxDrawdown(IReadOnlyList<EquityPoint> curve)
    {
        var peak = 0m;
        var maxDD = 0m;
        foreach (var p in curve)
        {
            if (p.Equity > peak) peak = p.Equity;
            if (peak != 0)
            {
                var dd = (peak - p.Equity) / peak;
                if (dd > maxDD) maxDD = dd;
            }
        }
        return (double)-maxDD;
    }

    private static double? ComputeSharpe(double[] returns, double riskFreeRate, int periodsPerYear)
    {
        if (returns.Length == 0 || periodsPerYear <= 0)
            return null;

        var mean = returns.Average();
        var variance = returns.Sum(r =>
        {
            var d = r - mean;
            return d * d;
        }) / returns.Length;
        var std = Math.Sqrt(variance);
        if (std == 0 || double.IsNaN(std))
            return null;

        var periodicRf = riskFreeRate / periodsPerYear;
        var sharpe = (mean - periodicRf) / std * Math.Sqrt(periodsPerYear);
        return double.IsNaN(sharpe) ? null : sharpe;
    }

    private sealed record Position(TradeSide Side, decimal EntryPrice, decimal Quantity, DateOnly EntryDate);
    private sealed record ComputedMetrics(double TotalReturn, double MaxDrawdown, double? Sharpe);
}
