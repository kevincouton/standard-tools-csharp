using StandardTools.Core;
using Xunit;

namespace StandardTools.Backtest.Tests;

public class BacktestEngineTests
{
    private static IReadOnlyList<OHLCV> RisingSeries(int n, decimal start, decimal step)
    {
        var bars = new List<OHLCV>(n);
        var price = start;
        for (var i = 0; i < n; i++)
        {
            var open = price;
            var close = open + step;
            bars.Add(MakeBar(i, open, close));
            price = close;
        }
        return bars;
    }

    private static IReadOnlyList<OHLCV> ConstantSeries(int n, decimal price)
    {
        return Enumerable.Range(0, n).Select(i => MakeBar(i, price, price)).ToArray();
    }

    private static IReadOnlyList<OHLCV> FallingSeries(int n, decimal start, decimal step)
    {
        var bars = new List<OHLCV>(n);
        var price = start;
        for (var i = 0; i < n; i++)
        {
            var open = price;
            var close = open - step;
            bars.Add(MakeBar(i, open, close));
            price = close;
        }
        return bars;
    }

    private static OHLCV MakeBar(int offset, decimal open, decimal close)
    {
        var high = Math.Max(open, close) + 0.5m;
        var low = Math.Min(open, close) - 0.5m;
        return new OHLCV(new Ticker("AAPL"), new DateOnly(2024, 1, 1).AddDays(offset), open, high, low, close, 1_000_000);
    }

    [Fact]
    public void BuyAndHold_TotalReturn()
    {
        var series = RisingSeries(10, 100, 1);
        var engine = new BacktestEngine("buy_and_hold", new BacktestConfig());
        var result = engine.Run(series);

        var wantReturn = 110.0m / 101.0m - 1;
        Assert.Equal((double)wantReturn, result.TotalReturn, 6);
        Assert.True(result.FinalEquity > 100_000m);
        Assert.Single(result.Trades);
        Assert.Equal(TradeSide.Long, result.Trades[0].Side);
    }

    [Fact]
    public void SmaCrossover_GeneratesTrades()
    {
        var flat = ConstantSeries(29, 100);
        var rise = RisingSeries(15, 100, 1);
        var series = flat.Take(flat.Count - 1).Concat(rise).ToArray();

        var engine = new BacktestEngine("sma_crossover", new BacktestConfig());
        var result = engine.Run(series, new Dictionary<string, string> { ["fast"] = "5", ["slow"] = "10" });

        Assert.NotEmpty(result.Trades);
        Assert.True(result.Metrics.TradeCount >= 1);
    }

    [Fact]
    public void EmptySeries_Throws()
    {
        var engine = new BacktestEngine("buy_and_hold", new BacktestConfig());
        Assert.Throws<InvalidCommandException>(() => engine.Run(Array.Empty<OHLCV>()));
    }

    [Fact]
    public void UnknownStrategy_Throws()
    {
        Assert.Throws<InvalidCommandException>(() => new BacktestEngine("unknown_strategy", new BacktestConfig()));
    }

    [Fact]
    public void RsiThreshold_Runs()
    {
        var decline = FallingSeries(30, 100, 1);
        var engine = new BacktestEngine("rsi_threshold", new BacktestConfig());
        var result = engine.Run(decline, new Dictionary<string, string> { ["period"] = "14", ["oversold"] = "30", ["overbought"] = "70" });

        Assert.NotNull(result.EquityCurve);
        Assert.True(result.EquityCurve.Count >= decline.Count);
    }

    [Fact]
    public void MonteCarlo_FromTrades()
    {
        var trade = new Trade(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 2), 100m, 110m, 10m, TradeSide.Long, 100m);
        var simulator = new MonteCarloSimulator(100, 42);
        var result = simulator.FromTrades(new[] { trade }, 100_000m);

        Assert.Equal(100, result.Simulations);
        Assert.True(result.FinalEquityCI.Lower <= result.FinalEquityCI.Upper);
    }

    [Fact]
    public void WalkForward_Runs()
    {
        var series = RisingSeries(60, 100, 0.5m);
        var request = new WalkForwardRequest(
            "sma_crossover",
            new Ticker("AAPL"),
            series,
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["fast"] = new[] { "5", "10" },
                ["slow"] = new[] { "20", "30" }
            },
            20,
            10,
            OptimizationMetric.TotalReturn,
            new BacktestConfig());

        var optimizer = new WalkForwardOptimizer(request);
        var result = optimizer.Run();

        Assert.NotEmpty(result.EquityCurve);
        Assert.NotEmpty(result.SelectedParams);
    }
}
