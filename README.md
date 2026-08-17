# standard-tools-csharp

C# / .NET port of the Standard-Tools quantitative finance toolkit.

## Stack

- .NET 10
- ASP.NET Core (REST)
- xUnit (tests)
- GitHub Actions (CI)

## Project Structure

```
src/
  StandardTools.Core/        Shared domain value objects and errors
  StandardTools.MarketData/  Market data provider port
  StandardTools.Indicators/  Technical indicators
  StandardTools.Metrics/     Risk/return metrics
  StandardTools.Analysis/    Statistical analysis (regression, PCA, options)
  StandardTools.Backtest/    Backtesting engine and strategies
  StandardTools.Portfolio/   Portfolio optimization
  StandardTools.Screener/    Stock screener
  StandardTools.Agent/       LLM/agent tool dispatcher
  StandardTools.Audit/       Hash-chained decision audit trail
  StandardTools.Api/         ASP.NET Core host and REST adapters

tests/
  StandardTools.*.Tests/     Unit and integration tests per module
```

## Quick Start

```bash
dotnet build
dotnet test
dotnet run --project src/StandardTools.Api
```

## Endpoints

- `GET /health` — health check
- `GET /api/v1/market-data/bars` — market data parameter parser (no live provider yet)
- `POST /api/v1/indicators/{indicator}` — compute indicator (`sma`, `ema`, `rsi`, `macd`, `bollinger_bands`, `atr`, `obv`, `vwap`)
- `POST /api/v1/metrics/risk` — compute risk metrics
- `POST /api/v1/metrics/return` — compute return metrics
- `POST /api/v1/analysis/{operation}` — analysis operations (`regression`, `options` exposed via REST; full library in `StandardTools.Analysis`)
- `POST /api/v1/backtest/run` — run backtest
- `POST /api/v1/portfolio/optimize` — portfolio optimization
- `POST /api/v1/screener` — stock screener
- `POST /api/v1/agent/tools` — dispatch agent tool
- `POST /api/v1/audit/verify` — verify audit chain

## Modules

All core quant modules are implemented with unit and integration tests:

- [x] Market data (provider port + parameter endpoint; no live data adapter yet)
- [x] Indicators
- [x] Metrics
- [x] Analysis
- [x] Backtest
- [x] Portfolio
- [x] Screener
- [x] Agent tools
- [x] Audit

## Security

API-key authentication is implemented and enabled by default. Set `SQT_API_KEY` to a strong secret; the server fails to start if auth is enabled and no key is configured. TLS is not yet implemented—deploy behind a reverse proxy that provides TLS termination.
