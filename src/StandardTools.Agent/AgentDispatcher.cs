using System.Text.Json;
using StandardTools.Analysis;
using StandardTools.Backtest;
using StandardTools.Core;
using StandardTools.Indicators;
using StandardTools.Metrics;
using StandardTools.Portfolio;
using StandardTools.Screener;

namespace StandardTools.Agent;

/// <summary>
/// Routes agent tool calls to domain services.
/// </summary>
public sealed class AgentDispatcher
{
    private readonly IFundamentalProvider? _fundamentals;
    private readonly IndicatorCalculator _indicators;
    private readonly MetricsCalculator _metrics;
    private readonly AnalysisCalculator _analysis;
    private readonly Screener.Screener? _screener;

    public AgentDispatcher(
        IFundamentalProvider? fundamentals = null,
        IndicatorCalculator? indicators = null,
        MetricsCalculator? metrics = null,
        AnalysisCalculator? analysis = null)
    {
        _fundamentals = fundamentals;
        _indicators = indicators ?? new IndicatorCalculator();
        _metrics = metrics ?? new MetricsCalculator();
        _analysis = analysis ?? new AnalysisCalculator();
        _screener = fundamentals is not null ? new Screener.Screener(fundamentals) : null;
    }

    public Task<ToolResult> DispatchAsync(ToolCall call, CancellationToken cancellationToken = default)
    {
        if (ToolRegistry.FindTool(call.Name) is null)
            return Task.FromResult(ToolResult.ErrorResult($"unknown tool {call.Name}"));

        return call.Name switch
        {
            ToolNames.Health => Task.FromResult(ToolResult.Ok(new { status = "ok" })),
            ToolNames.ListTools => Task.FromResult(ToolResult.Ok(ToolRegistry.ListTools().Select(t => t.Name).ToArray())),
            ToolNames.IndicatorsCalculate => Task.FromResult(IndicatorsCalculate(call.Arguments)),
            ToolNames.MetricsRisk => Task.FromResult(MetricsRisk(call.Arguments)),
            ToolNames.MetricsReturn => Task.FromResult(MetricsReturn(call.Arguments)),
            ToolNames.AnalysisRegression => Task.FromResult(AnalysisRegression(call.Arguments)),
            ToolNames.AnalysisOptions => Task.FromResult(AnalysisOptions(call.Arguments)),
            ToolNames.RunBuyAndHold => Task.FromResult(RunBacktest(call.Arguments, "buy_and_hold")),
            ToolNames.RunSmaCrossover => Task.FromResult(RunBacktest(call.Arguments, "sma_crossover")),
            ToolNames.RunWalkForward => Task.FromResult(RunWalkForward(call.Arguments)),
            ToolNames.RunMonteCarlo => Task.FromResult(RunMonteCarlo(call.Arguments)),
            ToolNames.PortfolioOptimize => Task.FromResult(PortfolioOptimize(call.Arguments)),
            ToolNames.RiskParity => Task.FromResult(RiskParity(call.Arguments)),
            ToolNames.BlackLitterman => Task.FromResult(BlackLitterman(call.Arguments)),
            ToolNames.RunScreener => RunScreenerAsync(call.Arguments, cancellationToken),
            _ => Task.FromResult(ToolResult.ErrorResult($"unknown tool {call.Name}"))
        };
    }

    private ToolResult IndicatorsCalculate(JsonElement args)
    {
        var payload = Deserialize<IndicatorPayload>(args);
        var series = BuildSeries(payload);
        var parameters = payload.Params ?? new Dictionary<string, string>();
        var result = _indicators.Calculate(payload.Indicator, series, parameters);
        return ToolResult.Ok(result);
    }

    private ToolResult MetricsRisk(JsonElement args)
    {
        var payload = Deserialize<ValuesPayload>(args);
        var (_, risk) = _metrics.Calculate(payload.Values);
        return ToolResult.Ok(risk);
    }

    private ToolResult MetricsReturn(JsonElement args)
    {
        var payload = Deserialize<ValuesPayload>(args);
        var (returns, _) = _metrics.Calculate(payload.Values);
        return ToolResult.Ok(returns);
    }

    private ToolResult AnalysisRegression(JsonElement args)
    {
        var payload = Deserialize<RegressionPayload>(args);
        var result = _analysis.LinearRegression(payload.BenchmarkReturns, payload.AssetReturns);
        return ToolResult.Ok(result);
    }

    private ToolResult AnalysisOptions(JsonElement args)
    {
        var payload = Deserialize<OptionsPayload>(args);
        var result = _analysis.Options(payload.OptionType, payload.Spot, payload.Strike, payload.RiskFreeRate, payload.Volatility, payload.TimeToMaturity);
        return ToolResult.Ok(result);
    }

    private ToolResult RunBacktest(JsonElement args, string defaultStrategy)
    {
        var (payload, series, config) = ParseBacktestArgs(args);
        var strategy = string.IsNullOrEmpty(defaultStrategy) ? payload.Strategy : defaultStrategy;
        var engine = new BacktestEngine(strategy, config);
        var result = engine.Run(series, payload.Params);
        return ToolResult.Ok(result);
    }

    private ToolResult RunWalkForward(JsonElement args)
    {
        var (payload, series, config) = ParseBacktestArgs(args);
        var wfPayload = Deserialize<WalkForwardPayload>(args);
        var request = new WalkForwardRequest(
            payload.Strategy,
            new Ticker("unknown"),
            series,
            wfPayload.ParamGrid ?? new Dictionary<string, IReadOnlyList<string>>(),
            wfPayload.TrainSize,
            wfPayload.TestSize,
            ParseMetric(wfPayload.Metric),
            config);
        var optimizer = new WalkForwardOptimizer(request);
        var result = optimizer.Run();
        return ToolResult.Ok(result);
    }

    private ToolResult RunMonteCarlo(JsonElement args)
    {
        var (payload, series, config) = ParseBacktestArgs(args);
        var mcPayload = Deserialize<MonteCarloPayload>(args);
        var engine = new BacktestEngine(payload.Strategy, config);
        var backtestResult = engine.Run(series, payload.Params);
        var simulator = new MonteCarloSimulator(mcPayload.Simulations, mcPayload.Seed);
        var result = simulator.FromTrades(backtestResult.Trades, config.InitialCapital);
        return ToolResult.Ok(result);
    }

    private ToolResult PortfolioOptimize(JsonElement args)
    {
        var payload = Deserialize<PortfolioOptimizePayload>(args);
        var request = new MeanVarianceRequest
        {
            Returns = payload.Returns,
            Labels = payload.Labels,
            RiskFreeRate = payload.RiskFreeRate,
            Objective = payload.Objective,
            TargetReturn = payload.TargetReturn,
            TargetVolatility = payload.TargetVolatility
        };
        var result = PortfolioOptimizer.MeanVariance(request);
        return ToolResult.Ok(result);
    }

    private ToolResult RiskParity(JsonElement args)
    {
        var payload = Deserialize<RiskParityPayload>(args);
        var request = new RiskParityRequest
        {
            Returns = payload.Returns,
            Labels = payload.Labels
        };
        var result = PortfolioOptimizer.RiskParity(request);
        return ToolResult.Ok(result);
    }

    private ToolResult BlackLitterman(JsonElement args)
    {
        var payload = Deserialize<BlackLittermanPayload>(args);
        var request = new BlackLittermanSimplifiedRequest
        {
            Returns = payload.Returns,
            Labels = payload.Labels,
            MarketCaps = payload.MarketCaps,
            Views = payload.Views,
            Tau = payload.Tau,
            RiskAversion = payload.RiskAversion
        };
        var (result, expected, covariance) = PortfolioOptimizer.BlackLittermanSimplified(request);
        return ToolResult.Ok(new
        {
            portfolio = result,
            expected_returns = expected,
            posterior_covariance = covariance
        });
    }

    private async Task<ToolResult> RunScreenerAsync(JsonElement args, CancellationToken cancellationToken)
    {
        if (_screener is null)
            return ToolResult.ErrorResult("screener is not configured with a fundamental provider");

        var payload = Deserialize<ScreenerPayload>(args);
        var result = await _screener.ScreenAsync(payload.Tickers, payload.Criteria ?? new ScreenCriteria(), cancellationToken).ConfigureAwait(false);
        return ToolResult.Ok(result);
    }

    private (BacktestPayload Payload, IReadOnlyList<OHLCV> Series, BacktestConfig Config) ParseBacktestArgs(JsonElement args)
    {
        var payload = Deserialize<BacktestPayload>(args);
        var series = BuildSeries(payload);
        var config = new BacktestConfig(
            payload.InitialCapital > 0 ? payload.InitialCapital : 100_000m,
            payload.CommissionRate);
        return (payload, series, config);
    }

    private static IReadOnlyList<OHLCV> BuildSeries(IndicatorPayload payload)
    {
        var count = payload.Dates.Count;
        if (payload.Opens.Count != count || payload.Highs.Count != count || payload.Lows.Count != count || payload.Closes.Count != count)
            throw new InvalidCommandException("all price arrays must have the same length");

        var ticker = new Ticker("unknown");
        var series = new List<OHLCV>(count);
        for (var i = 0; i < count; i++)
        {
            series.Add(new OHLCV(
                ticker,
                payload.Dates[i],
                payload.Opens[i],
                payload.Highs[i],
                payload.Lows[i],
                payload.Closes[i],
                i < payload.Volumes.Count ? payload.Volumes[i] : 0));
        }
        return series;
    }

    private static IReadOnlyList<OHLCV> BuildSeries(BacktestPayload payload)
    {
        var count = payload.Dates.Count;
        if (payload.Closes.Count != count)
            throw new InvalidCommandException("dates and closes must have the same length");

        var ticker = new Ticker("unknown");
        var series = new List<OHLCV>(count);
        for (var i = 0; i < count; i++)
        {
            var close = payload.Closes[i];
            series.Add(new OHLCV(ticker, payload.Dates[i], close, close, close, close, 0));
        }
        return series;
    }

    private static OptimizationMetric ParseMetric(string? metric) => metric?.ToLowerInvariant() switch
    {
        "sharpe" => OptimizationMetric.Sharpe,
        "win_rate" => OptimizationMetric.WinRate,
        _ => OptimizationMetric.TotalReturn
    };

    private static T Deserialize<T>(JsonElement element) =>
        JsonSerializer.Deserialize<T>(element, AgentJsonOptions.Instance)
        ?? throw new InvalidCommandException("invalid or empty arguments");

    private sealed class IndicatorPayload
    {
        public List<DateOnly> Dates { get; set; } = new();
        public List<decimal> Opens { get; set; } = new();
        public List<decimal> Highs { get; set; } = new();
        public List<decimal> Lows { get; set; } = new();
        public List<decimal> Closes { get; set; } = new();
        public List<long> Volumes { get; set; } = new();
        public string Indicator { get; set; } = string.Empty;
        public Dictionary<string, string>? Params { get; set; }
    }

    private sealed class ValuesPayload
    {
        public List<double> Values { get; set; } = new();
        public double RiskFreeRate { get; set; }
    }

    private sealed class RegressionPayload
    {
        public List<double> AssetReturns { get; set; } = new();
        public List<double> BenchmarkReturns { get; set; } = new();
    }

    private sealed class OptionsPayload
    {
        public double Spot { get; set; }
        public double Strike { get; set; }
        public double RiskFreeRate { get; set; }
        public double Volatility { get; set; }
        public double TimeToMaturity { get; set; }
        public string OptionType { get; set; } = string.Empty;
    }

    private sealed class BacktestPayload
    {
        public List<DateOnly> Dates { get; set; } = new();
        public List<decimal> Closes { get; set; } = new();
        public string Strategy { get; set; } = string.Empty;
        public Dictionary<string, string>? Params { get; set; }
        public decimal InitialCapital { get; set; }
        public double CommissionRate { get; set; }
    }

    private sealed class WalkForwardPayload
    {
        public int TrainSize { get; set; }
        public int TestSize { get; set; }
        public Dictionary<string, IReadOnlyList<string>>? ParamGrid { get; set; }
        public string? Metric { get; set; }
    }

    private sealed class MonteCarloPayload
    {
        public int Simulations { get; set; }
        public int? Seed { get; set; }
    }

    private sealed class PortfolioOptimizePayload
    {
        public List<List<double>> Returns { get; set; } = new();
        public List<string> Labels { get; set; } = new();
        public double RiskFreeRate { get; set; }
        public string Objective { get; set; } = string.Empty;
        public double? TargetReturn { get; set; }
        public double? TargetVolatility { get; set; }
    }

    private sealed class RiskParityPayload
    {
        public List<List<double>> Returns { get; set; } = new();
        public List<string> Labels { get; set; } = new();
    }

    private sealed class BlackLittermanPayload
    {
        public List<List<double>> Returns { get; set; } = new();
        public List<string> Labels { get; set; } = new();
        public Dictionary<string, double> MarketCaps { get; set; } = new();
        public Dictionary<string, double> Views { get; set; } = new();
        public double Tau { get; set; } = 0.05;
        public double RiskAversion { get; set; } = 2.5;
    }

    private sealed class ScreenerPayload
    {
        public List<string> Tickers { get; set; } = new();
        public ScreenCriteria? Criteria { get; set; }
    }
}
