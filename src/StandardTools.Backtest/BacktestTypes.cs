using StandardTools.Core;

namespace StandardTools.Backtest;

public enum SignalType { Hold, Buy, Sell }

public enum TradeSide { Long, Short }

public sealed record SignalResult(DateOnly Date, SignalType Signal);

public sealed record Trade(
    DateOnly EntryDate,
    DateOnly ExitDate,
    decimal EntryPrice,
    decimal ExitPrice,
    decimal Quantity,
    TradeSide Side,
    decimal PnL);

public sealed record EquityPoint(DateOnly Date, decimal Equity);

public sealed record BacktestMetrics(
    double MaxDrawdown,
    double? Sharpe,
    double WinRate,
    int TradeCount);

public sealed record BacktestResult(
    decimal FinalEquity,
    double TotalReturn,
    IReadOnlyList<EquityPoint> EquityCurve,
    IReadOnlyList<Trade> Trades,
    BacktestMetrics Metrics);

public sealed record BacktestConfig(
    decimal InitialCapital = 100_000m,
    double CommissionRate = 0.0,
    int PeriodsPerYear = 252,
    double RiskFreeRate = 0.0);

public sealed record BacktestRequest(
    string Strategy,
    Ticker Ticker,
    IReadOnlyList<OHLCV> Series,
    IReadOnlyDictionary<string, string> Params,
    BacktestConfig Config);
