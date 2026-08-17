using System.Text.Json;
using System.Text.Json.Serialization;
using StandardTools.Agent;
using StandardTools.Analysis;
using StandardTools.Api;
using StandardTools.Audit;
using StandardTools.Backtest;
using StandardTools.Core;
using StandardTools.Indicators;
using StandardTools.Metrics;
using StandardTools.Portfolio;
using StandardTools.Screener;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

// Register domain services as singletons.
builder.Services.AddSingleton<IndicatorCalculator>();
builder.Services.AddSingleton<MetricsCalculator>();
builder.Services.AddSingleton<AnalysisCalculator>();
builder.Services.AddSingleton<IAuditStorage, InMemoryAuditStorage>();
builder.Services.AddSingleton<AuditWriter>();
builder.Services.AddSingleton<AuditVerifier>();
builder.Services.AddSingleton<AuditReplay>();
builder.Services.AddSingleton<AgentDispatcher>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/v1/market-data/bars", (
    string symbol,
    DateOnly startDate,
    DateOnly endDate,
    string interval,
    string? exchange,
    string? provider) =>
{
    try
    {
        var ticker = new Ticker(symbol, exchange);
        var range = new DateRange(startDate, endDate);
        var barInterval = ParseBarInterval(interval);
        return Results.Ok(new { ticker, range, interval = barInterval, provider });
    }
    catch (InvalidCommandException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Indicators
app.MapPost("/api/v1/indicators/{indicator}", (string indicator, IndicatorRequest request, IndicatorCalculator calculator) =>
{
    try
    {
        var series = BuildOhlcv(request.Dates, request.Opens, request.Highs, request.Lows, request.Closes, request.Volumes);
        var result = calculator.Calculate(indicator, series, request.Params);
        return Results.Ok(result);
    }
    catch (QuantException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Metrics
app.MapPost("/api/v1/metrics/risk", (ValuesRequest request, MetricsCalculator calculator) =>
{
    try
    {
        var calc = request.RiskFreeRate.HasValue ? new MetricsCalculator(request.RiskFreeRate.Value) : calculator;
        var (_, risk) = calc.Calculate(request.Values);
        return Results.Ok(risk);
    }
    catch (QuantException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/v1/metrics/return", (ValuesRequest request, MetricsCalculator calculator) =>
{
    try
    {
        var calc = request.RiskFreeRate.HasValue ? new MetricsCalculator(request.RiskFreeRate.Value) : calculator;
        var (returns, _) = calc.Calculate(request.Values);
        return Results.Ok(returns);
    }
    catch (QuantException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Analysis
app.MapPost("/api/v1/analysis/regression", (RegressionRequest request, AnalysisCalculator calculator) =>
{
    try
    {
        var result = calculator.LinearRegression(request.BenchmarkReturns, request.AssetReturns);
        return Results.Ok(result);
    }
    catch (QuantException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/v1/analysis/options", (OptionsRequest request, AnalysisCalculator calculator) =>
{
    try
    {
        var result = calculator.Options(request.OptionType, request.Spot, request.Strike, request.RiskFreeRate, request.Volatility, request.TimeToMaturity);
        return Results.Ok(result);
    }
    catch (QuantException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Backtest
app.MapPost("/api/v1/backtest/{strategy}", (string strategy, BacktestRequest request) =>
{
    try
    {
        var series = BuildOhlcvFromCloses(request.Dates, request.Closes);
        var config = new BacktestConfig(request.InitialCapital ?? 100_000m, request.CommissionRate ?? 0.0);
        var engine = new BacktestEngine(strategy, config);
        var result = engine.Run(series, request.Params);
        return Results.Ok(result);
    }
    catch (QuantException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/v1/backtest/walk-forward", (WalkForwardRequest request) =>
{
    try
    {
        var series = BuildOhlcvFromCloses(request.Dates, request.Closes);
        var config = new BacktestConfig(request.InitialCapital ?? 100_000m, request.CommissionRate ?? 0.0);
        var wfRequest = new StandardTools.Backtest.WalkForwardRequest(
            request.Strategy,
            new Ticker("unknown"),
            series,
            request.ParamGrid ?? new Dictionary<string, IReadOnlyList<string>>(),
            request.TrainSize,
            request.TestSize,
            ParseOptimizationMetric(request.Metric),
            config);
        var optimizer = new WalkForwardOptimizer(wfRequest);
        var result = optimizer.Run();
        return Results.Ok(result);
    }
    catch (QuantException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/v1/backtest/monte-carlo", (MonteCarloRequest request) =>
{
    try
    {
        var series = BuildOhlcvFromCloses(request.Dates, request.Closes);
        var config = new BacktestConfig(request.InitialCapital ?? 100_000m, request.CommissionRate ?? 0.0);
        var engine = new BacktestEngine(request.Strategy, config);
        var backtestResult = engine.Run(series, request.Params);
        var simulator = new MonteCarloSimulator(request.Simulations, request.Seed);
        var result = simulator.FromTrades(backtestResult.Trades, config.InitialCapital);
        return Results.Ok(result);
    }
    catch (QuantException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Portfolio
app.MapPost("/api/v1/portfolio/optimize", (MeanVarianceRequest request) =>
{
    try
    {
        var result = PortfolioOptimizer.MeanVariance(request);
        return Results.Ok(result);
    }
    catch (QuantException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/v1/portfolio/risk-parity", (RiskParityRequest request) =>
{
    try
    {
        var result = PortfolioOptimizer.RiskParity(request);
        return Results.Ok(result);
    }
    catch (QuantException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/v1/portfolio/black-litterman", (BlackLittermanSimplifiedRequest request) =>
{
    try
    {
        var (result, expected, covariance) = PortfolioOptimizer.BlackLittermanSimplified(request);
        return Results.Ok(new { portfolio = result, expected_returns = expected, posterior_covariance = covariance });
    }
    catch (QuantException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Screener
app.MapPost("/api/v1/screener", async (ScreenerRequest request, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.ProviderType))
        return Results.BadRequest(new { error = "provider_type is required" });

    var provider = CreateFundamentalProvider(request.ProviderType, request.ProviderConfig);
    if (provider is null)
        return Results.BadRequest(new { error = $"unknown provider_type {request.ProviderType}" });

    var screener = new StandardTools.Screener.Screener(provider);
    var result = await screener.ScreenAsync(request.Tickers, request.Criteria ?? new ScreenCriteria(), cancellationToken);
    return Results.Ok(result);
});

// Agent tools (A2A/MCP-style single endpoint)
app.MapPost("/api/v1/agent/tools", async (ToolCallRequest request, AgentDispatcher dispatcher, CancellationToken cancellationToken) =>
{
    var call = new ToolCall { Name = request.Name, Arguments = request.Arguments };
    var result = await dispatcher.DispatchAsync(call, cancellationToken);
    return result.Error is null ? Results.Ok(result.Output) : Results.BadRequest(result.Output);
});

app.MapGet("/api/v1/agent/tools", () => Results.Ok(ToolRegistry.ListTools().Select(t => new { t.Name, t.Description, t.Parameters })));

// Audit
app.MapPost("/api/v1/audit/record", async (DecisionRecord record, AuditWriter writer, CancellationToken cancellationToken) =>
{
    try
    {
        await writer.WriteAsync(record, cancellationToken);
        return Results.Ok(new { status = "recorded" });
    }
    catch (QuantException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/v1/audit/verify", async (AuditVerifier verifier, CancellationToken cancellationToken) =>
{
    try
    {
        await verifier.VerifyChainAsync(cancellationToken);
        return Results.Ok(new { status = "valid" });
    }
    catch (AuditIntegrityException ex)
    {
        return Results.BadRequest(new { status = "invalid", error = ex.Message });
    }
});

app.MapGet("/api/v1/audit/replay/{requestId}", async (string requestId, AuditReplay replay, CancellationToken cancellationToken) =>
{
    try
    {
        var record = await replay.LoadAsync(requestId, cancellationToken);
        return Results.Ok(record);
    }
    catch (QuantException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();

static BarInterval ParseBarInterval(string interval)
{
    return interval.ToUpperInvariant() switch
    {
        "DAILY" or "D" => BarInterval.Daily,
        "WEEKLY" or "W" => BarInterval.Weekly,
        "MONTHLY" or "M" => BarInterval.Monthly,
        _ => throw new InvalidCommandException($"Unknown interval: {interval}")
    };
}

static OptimizationMetric ParseOptimizationMetric(string? metric) => metric?.ToLowerInvariant() switch
{
    "sharpe" => OptimizationMetric.Sharpe,
    "win_rate" => OptimizationMetric.WinRate,
    _ => OptimizationMetric.TotalReturn
};

static IReadOnlyList<OHLCV> BuildOhlcv(List<DateOnly> dates, List<decimal> opens, List<decimal> highs, List<decimal> lows, List<decimal> closes, List<long>? volumes)
{
    var ticker = new Ticker("unknown");
    var result = new List<OHLCV>(dates.Count);
    for (var i = 0; i < dates.Count; i++)
    {
        result.Add(new OHLCV(
            ticker,
            dates[i],
            opens[i],
            highs[i],
            lows[i],
            closes[i],
            volumes is not null && i < volumes.Count ? volumes[i] : 0));
    }
    return result;
}

static IReadOnlyList<OHLCV> BuildOhlcvFromCloses(List<DateOnly> dates, List<decimal> closes)
{
    var ticker = new Ticker("unknown");
    return dates.Select((date, i) =>
    {
        var close = closes[i];
        return new OHLCV(ticker, date, close, close, close, close, 0);
    }).ToArray();
}

static IFundamentalProvider? CreateFundamentalProvider(string providerType, JsonElement? config) => providerType.ToLowerInvariant() switch
{
    "hardcoded" => new HardcodedFundamentalProvider(config),
    _ => null
};

public partial class Program { }

// Request records
public sealed record IndicatorRequest(
    List<DateOnly> Dates,
    List<decimal> Opens,
    List<decimal> Highs,
    List<decimal> Lows,
    List<decimal> Closes,
    List<long>? Volumes,
    Dictionary<string, string>? Params);

public sealed record ValuesRequest(List<double> Values, double? RiskFreeRate);
public sealed record RegressionRequest(List<double> AssetReturns, List<double> BenchmarkReturns);
public sealed record OptionsRequest(
    string OptionType,
    double Spot,
    double Strike,
    double RiskFreeRate,
    double Volatility,
    double TimeToMaturity);

public sealed record BacktestRequest(
    List<DateOnly> Dates,
    List<decimal> Closes,
    Dictionary<string, string>? Params,
    decimal? InitialCapital,
    double? CommissionRate);

public sealed record WalkForwardRequest(
    List<DateOnly> Dates,
    List<decimal> Closes,
    string Strategy,
    int TrainSize,
    int TestSize,
    Dictionary<string, IReadOnlyList<string>>? ParamGrid,
    string? Metric,
    decimal? InitialCapital,
    double? CommissionRate);

public sealed record MonteCarloRequest(
    List<DateOnly> Dates,
    List<decimal> Closes,
    string Strategy,
    int Simulations,
    int? Seed,
    Dictionary<string, string>? Params,
    decimal? InitialCapital,
    double? CommissionRate);

public sealed record ScreenerRequest
{
    [JsonPropertyName("tickers")]
    public required List<string> Tickers { get; init; }

    [JsonPropertyName("criteria")]
    public ScreenCriteria? Criteria { get; init; }

    [JsonPropertyName("provider_type")]
    public required string ProviderType { get; init; }

    [JsonPropertyName("provider_config")]
    public JsonElement ProviderConfig { get; init; }
}

public sealed record ToolCallRequest(string Name, JsonElement Arguments);
