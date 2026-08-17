using System.Text.Json;
using StandardTools.Agent;
using StandardTools.Screener;
using Xunit;

namespace StandardTools.Agent.Tests;

public class AgentDispatcherTests
{
    private static AgentDispatcher Dispatcher => new();

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var result = await Dispatcher.DispatchAsync(new ToolCall { Name = ToolNames.Health, Arguments = JsonDocument.Parse("{}").RootElement });
        Assert.Null(result.Error);
        Assert.Equal("ok", result.Output.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ListTools_ReturnsNames()
    {
        var result = await Dispatcher.DispatchAsync(new ToolCall { Name = ToolNames.ListTools, Arguments = JsonDocument.Parse("{}").RootElement });
        Assert.Null(result.Error);
        var names = result.Output.EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Contains(ToolNames.Health, names);
        Assert.Contains(ToolNames.PortfolioOptimize, names);
    }

    [Fact]
    public async Task UnknownTool_ReturnsError()
    {
        var result = await Dispatcher.DispatchAsync(new ToolCall { Name = "unknown", Arguments = JsonDocument.Parse("{}").RootElement });
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task IndicatorsCalculate_Sma()
    {
        var args = JsonSerializer.SerializeToElement(new
        {
            dates = new[] { "2026-01-01", "2026-01-02", "2026-01-03", "2026-01-04", "2026-01-05" },
            opens = new[] { 100m, 101m, 102m, 103m, 104m },
            highs = new[] { 100m, 101m, 102m, 103m, 104m },
            lows = new[] { 100m, 101m, 102m, 103m, 104m },
            closes = new[] { 100m, 101m, 102m, 103m, 104m },
            volumes = new[] { 1000L, 1000L, 1000L, 1000L, 1000L },
            indicator = "sma",
            param = new Dictionary<string, string> { ["period"] = "3" }
        });

        var result = await Dispatcher.DispatchAsync(new ToolCall { Name = ToolNames.IndicatorsCalculate, Arguments = args });
        Assert.Null(result.Error);
        Assert.Equal("sma", result.Output.GetProperty("name").GetString());
    }

    [Fact]
    public async Task MetricsRisk_ComputesSharpe()
    {
        var args = JsonSerializer.SerializeToElement(new { values = new[] { 100.0, 101.0, 102.0, 101.0, 103.0 } });
        var result = await Dispatcher.DispatchAsync(new ToolCall { Name = ToolNames.MetricsRisk, Arguments = args });
        Assert.Null(result.Error);
        Assert.True(result.Output.GetProperty("sharpe_ratio").ValueKind == JsonValueKind.Number);
    }

    [Fact]
    public async Task AnalysisRegression_ReturnsSlope()
    {
        var args = JsonSerializer.SerializeToElement(new
        {
            asset_returns = new[] { 0.01, 0.02, -0.01, 0.005 },
            benchmark_returns = new[] { 0.005, 0.015, -0.005, 0.0 }
        });
        var result = await Dispatcher.DispatchAsync(new ToolCall { Name = ToolNames.AnalysisRegression, Arguments = args });
        Assert.Null(result.Error);
        Assert.True(result.Output.GetProperty("slope").ValueKind == JsonValueKind.Number);
    }

    [Fact]
    public async Task AnalysisOptions_CallPrice()
    {
        var args = JsonSerializer.SerializeToElement(new
        {
            spot = 100.0,
            strike = 100.0,
            risk_free_rate = 0.05,
            volatility = 0.2,
            time_to_maturity = 1.0,
            option_type = "call"
        });
        var result = await Dispatcher.DispatchAsync(new ToolCall { Name = ToolNames.AnalysisOptions, Arguments = args });
        Assert.Null(result.Error);
        Assert.True(result.Output.GetProperty("price").GetDouble() > 0);
    }

    [Fact]
    public async Task RunBuyAndHold_ReturnsMetrics()
    {
        var args = JsonSerializer.SerializeToElement(new
        {
            dates = new[] { "2026-01-01", "2026-01-02", "2026-01-03", "2026-01-04", "2026-01-05" },
            closes = new[] { 100m, 101m, 102m, 103m, 104m }
        });
        var result = await Dispatcher.DispatchAsync(new ToolCall { Name = ToolNames.RunBuyAndHold, Arguments = args });
        Assert.Null(result.Error);
        Assert.True(result.Output.GetProperty("metrics").GetProperty("trade_count").ValueKind == JsonValueKind.Number);
    }

    [Fact]
    public async Task PortfolioOptimize_MaxSharpe()
    {
        var args = JsonSerializer.SerializeToElement(new
        {
            returns = new[] { new[] { 0.01, 0.02, -0.01 }, new[] { 0.005, 0.015, 0.0 } },
            labels = new[] { "A", "B" },
            objective = "max_sharpe"
        });
        var result = await Dispatcher.DispatchAsync(new ToolCall { Name = ToolNames.PortfolioOptimize, Arguments = args });
        Assert.Null(result.Error);
        Assert.True(result.Output.GetProperty("expected_return").ValueKind == JsonValueKind.Number);
    }

    [Fact]
    public async Task RiskParity_ReturnsWeights()
    {
        var args = JsonSerializer.SerializeToElement(new
        {
            returns = new[] { new[] { 0.01, -0.01, 0.02 }, new[] { 0.005, 0.0, 0.015 } },
            labels = new[] { "A", "B" }
        });
        var result = await Dispatcher.DispatchAsync(new ToolCall { Name = ToolNames.RiskParity, Arguments = args });
        Assert.Null(result.Error);
        Assert.True(result.Output.GetProperty("weights").GetProperty("A").ValueKind == JsonValueKind.Number);
    }

    [Fact]
    public async Task RunScreener_WithoutProvider_ReturnsError()
    {
        var args = JsonSerializer.SerializeToElement(new
        {
            tickers = new[] { "AAPL" },
            criteria = new { }
        });
        var result = await Dispatcher.DispatchAsync(new ToolCall { Name = ToolNames.RunScreener, Arguments = args });
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task RunScreener_WithProvider_ReturnsMatches()
    {
        var provider = new HardcodedFundamentalProvider(new Dictionary<string, FundamentalData>
        {
            ["AAPL"] = new()
            {
                Ticker = "AAPL",
                MarketCap = 3e12,
                PERatio = 20,
                PBRatio = 5,
                DividendYield = 0.01,
                EPSGrowth = 0.1,
                DebtToEquity = 0.5,
                ROE = 0.3
            }
        });
        var dispatcher = new AgentDispatcher(fundamentals: provider);
        var args = JsonSerializer.SerializeToElement(new
        {
            tickers = new[] { "AAPL" },
            criteria = new { pe_ratio_max = 25.0 }
        });
        var result = await dispatcher.DispatchAsync(new ToolCall { Name = ToolNames.RunScreener, Arguments = args });
        Assert.Null(result.Error);
        Assert.Single(result.Output.GetProperty("matches").EnumerateArray());
    }

    private sealed class HardcodedFundamentalProvider : IFundamentalProvider
    {
        private readonly IReadOnlyDictionary<string, FundamentalData> _data;
        public HardcodedFundamentalProvider(IReadOnlyDictionary<string, FundamentalData> data) => _data = data;
        public Task<FundamentalData?> FetchAsync(string ticker, CancellationToken cancellationToken = default) =>
            Task.FromResult(_data.TryGetValue(ticker, out var d) ? d : null);
    }
}
