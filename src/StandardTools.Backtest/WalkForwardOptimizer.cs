using StandardTools.Core;

namespace StandardTools.Backtest;

public enum OptimizationMetric { TotalReturn, Sharpe, WinRate }

public sealed record ParamWindow(DateOnly Start, IReadOnlyDictionary<string, string> Params);

public sealed record WalkForwardResult(
    IReadOnlyList<EquityPoint> EquityCurve,
    IReadOnlyList<Trade> Trades,
    double TotalReturn,
    double MaxDrawdown,
    double? Sharpe,
    int NumberOfTrades,
    double WinRate,
    IReadOnlyList<ParamWindow> SelectedParams);

public sealed record WalkForwardRequest(
    string Strategy,
    Ticker Ticker,
    IReadOnlyList<OHLCV> Series,
    IReadOnlyDictionary<string, IReadOnlyList<string>> ParamGrid,
    int TrainSize,
    int TestSize,
    OptimizationMetric Metric,
    BacktestConfig Config);

/// <summary>
/// Splits a series into training and test windows, optimizes parameters in-sample,
/// and returns the combined out-of-sample result.
/// </summary>
public sealed class WalkForwardOptimizer
{
    private readonly WalkForwardRequest _request;

    public WalkForwardOptimizer(WalkForwardRequest request)
    {
        _request = request;
        if (request.TrainSize <= 0 || request.TestSize <= 0)
            throw new InvalidCommandException("Train and test sizes must be positive");
        StrategyFactory.Create(request.Strategy);
    }

    public WalkForwardResult Run()
    {
        var series = _request.Series;
        if (series.Count < _request.TrainSize + _request.TestSize)
            throw new InvalidCommandException("Series is too short for walk-forward configuration");

        var combinations = BuildParamCombinations(_request.ParamGrid);
        if (combinations.Count == 0)
            throw new InvalidCommandException("Walk-forward requires a non-empty parameter grid");

        var testResults = new List<BacktestResult>();
        var selectedParams = new List<ParamWindow>();
        var start = 0;

        while (start + _request.TrainSize + _request.TestSize <= series.Count)
        {
            var trainEnd = start + _request.TrainSize;
            var testEnd = trainEnd + _request.TestSize;
            var train = series.Skip(start).Take(_request.TrainSize).ToArray();
            var test = series.Skip(trainEnd).Take(_request.TestSize).ToArray();

            var bestParams = Optimize(train, combinations);
            var engine = new BacktestEngine(_request.Strategy, _request.Config);
            var result = engine.Run(test, bestParams);

            selectedParams.Add(new ParamWindow(test[0].Date, bestParams));
            testResults.Add(result);

            start += _request.TestSize;
        }

        return CombineResults(testResults, selectedParams);
    }

    private IReadOnlyDictionary<string, string> Optimize(IReadOnlyList<OHLCV> train, IReadOnlyList<IReadOnlyDictionary<string, string>> combinations)
    {
        double bestScore = 0;
        IReadOnlyDictionary<string, string>? bestParams = null;
        var found = false;

        foreach (var parameters in combinations)
        {
            var engine = new BacktestEngine(_request.Strategy, _request.Config);
            try
            {
                var result = engine.Run(train, parameters);
                var score = ScoreResult(result, _request.Metric);
                if (!found || score > bestScore)
                {
                    bestScore = score;
                    bestParams = parameters;
                    found = true;
                }
            }
            catch
            {
                // Skip invalid parameter combinations.
            }
        }

        if (!found || bestParams is null)
            throw new DataQualityException("No parameter combination produced a result");
        return bestParams;
    }

    private static double ScoreResult(BacktestResult result, OptimizationMetric metric) => metric switch
    {
        OptimizationMetric.Sharpe => result.Metrics.Sharpe ?? 0.0,
        OptimizationMetric.WinRate => result.Metrics.WinRate,
        _ => result.TotalReturn
    };

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> BuildParamCombinations(IReadOnlyDictionary<string, IReadOnlyList<string>> grid)
    {
        var keys = grid.Keys.ToList();
        var combinations = new List<IReadOnlyDictionary<string, string>> { new Dictionary<string, string>() };

        foreach (var key in keys)
        {
            var values = grid[key];
            if (values.Count == 0) continue;
            var next = new List<IReadOnlyDictionary<string, string>>();
            foreach (var baseParams in combinations)
            {
                foreach (var value in values)
                {
                    var extended = new Dictionary<string, string>(baseParams) { [key] = value };
                    next.Add(extended);
                }
            }
            combinations = next;
        }

        return combinations;
    }

    private WalkForwardResult CombineResults(IReadOnlyList<BacktestResult> results, IReadOnlyList<ParamWindow> selectedParams)
    {
        if (results.Count == 0)
            throw new InvalidCommandException("No out-of-sample windows were generated");

        var equityCurve = new List<EquityPoint>();
        var trades = new List<Trade>();
        decimal cumulativeCapital = 0;

        for (var i = 0; i < results.Count; i++)
        {
            var result = results[i];
            var windowInitial = result.EquityCurve[0].Equity;
            var windowFinal = result.EquityCurve[^1].Equity;
            var scale = i == 0
                ? (windowInitial == 0 ? 0m : _request.Config.InitialCapital / windowInitial)
                : (windowInitial == 0 ? 0m : cumulativeCapital / windowInitial);

            foreach (var p in result.EquityCurve)
                equityCurve.Add(new EquityPoint(p.Date, p.Equity * scale));
            foreach (var t in result.Trades)
                trades.Add(new Trade(t.EntryDate, t.ExitDate, t.EntryPrice, t.ExitPrice, t.Quantity, t.Side, t.PnL * scale));

            cumulativeCapital = windowFinal * scale;
        }

        equityCurve = DedupeEquityCurve(equityCurve);

        var engine = new BacktestEngine("buy_and_hold", _request.Config);
        var metrics = ComputeMetricsHelper(equityCurve, _request.Config);
        var winRate = trades.Count == 0 ? 0.0 : (double)trades.Count(t => t.PnL > 0) / trades.Count;

        return new WalkForwardResult(
            equityCurve,
            trades,
            metrics.TotalReturn,
            metrics.MaxDrawdown,
            metrics.Sharpe,
            trades.Count,
            winRate,
            selectedParams);
    }

    private static List<EquityPoint> DedupeEquityCurve(IReadOnlyList<EquityPoint> curve)
    {
        var seen = new HashSet<DateOnly>();
        var result = new List<EquityPoint>(curve.Count);
        foreach (var p in curve)
        {
            if (seen.Add(p.Date))
                result.Add(p);
        }
        return result;
    }

    private static ComputedMetrics ComputeMetricsHelper(IReadOnlyList<EquityPoint> curve, BacktestConfig config)
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
        var sharpe = (mean - riskFreeRate / periodsPerYear) / std * Math.Sqrt(periodsPerYear);
        return double.IsNaN(sharpe) ? null : sharpe;
    }

    private sealed record ComputedMetrics(double TotalReturn, double MaxDrawdown, double? Sharpe);
}
