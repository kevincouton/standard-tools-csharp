# standard-tools-csharp

C# / .NET port of the Standard-Tools quantitative finance toolkit.

## Stack

- .NET 10
- ASP.NET Core (REST + gRPC skeleton)
- xUnit (tests)
- GitHub Actions (CI)

## Project Structure

```
src/
  StandardTools.Core/        Shared domain value objects and errors
  StandardTools.MarketData/  Market data provider port
  StandardTools.Api/         ASP.NET Core host and adapters

tests/
  StandardTools.Core.Tests/  Unit tests for Core
```

## Quick Start

```bash
dotnet build
dotnet test
dotnet run --project src/StandardTools.Api
```

## Endpoints

- `GET /health` — health check
- `GET /api/v1/market-data/bars` — market data bars skeleton

## Roadmap

This is a skeleton. Planned modules mirror the Kotlin/Rust ports:

- [ ] Indicators
- [ ] Metrics
- [ ] Analysis
- [ ] Backtest
- [ ] Portfolio
- [ ] Screener
- [ ] Agent tools
- [ ] Audit
