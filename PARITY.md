# Port Parity Matrix

This document compares the `standard-tools-csharp` port against the other Standard-Tools language implementations. It is accurate as of the latest commit on `main`.

## Legend

- ✅ Implemented / available
- ⚠️ Partial, stub, or minimal implementation
- ❌ Not implemented
- N/A Not applicable for this transport/stack

## Transport & protocol support

| Feature | C# | Kotlin | Go | Rust | C++ |
|---|---|---|---|---|---|
| REST | ✅ | ✅ | ✅ | ✅ | ✅ |
| gRPC | ❌ | ✅ | ⚠️ health only | ⚠️ health + agent | ⚠️ health only |
| A2A | ❌ | ⚠️ tasks/send, no streaming | ⚠️ minimal | ⚠️ partial (get/cancel placeholders) | ⚠️ skeleton |
| MCP | ❌ | ✅ SSE | ⚠️ HTTP-only | ⚠️ HTTP-only | ⚠️ HTTP-only |
| SSE | ❌ | ⚠️ MCP transport only | ❌ | ❌ | ❌ |
| Docker / container image | ❌ | ✅ | ✅ | ✅ | ✅ |
| CLI | ❌ | ⚠️ audit commands only | ✅ | ⚠️ server + audit placeholders | ✅ |
| Container health checks | ⚠️ HTTP only | ⚠️ actuator only | ✅ | ❌ | ✅ |

## Domain modules

| Feature | C# | Kotlin | Go | Rust | C++ |
|---|---|---|---|---|---|
| Market data provider port | ⚠️ interface / stub | ✅ YF, Polygon, Bloomberg stub | ✅ synthetic, YF, Polygon | ✅ YF + Moka cache | ⚠️ synthetic only |
| Indicators | ✅ | ✅ | ✅ | ✅ | ✅ |
| Risk / return metrics | ✅ | ✅ | ✅ | ✅ | ✅ |
| Analysis (regression, cointegration, Hurst, PCA, correlation, options) | ✅ library; ⚠️ only regression + options exposed | ✅ | ⚠️ no multi-factor | ✅ | ⚠️ no multi-factor |
| Backtesting engine | ✅ | ✅ | ✅ | ✅ | ✅ |
| Walk-forward optimization | ✅ | ✅ | ✅ | ✅ | ✅ |
| Monte Carlo simulation | ✅ | ✅ | ✅ | ✅ | ✅ |
| Robustness / stress testing | ❌ | ✅ | ❌ | ✅ | ❌ |
| Portfolio mean-variance | ✅ | ✅ | ✅ | ✅ | ✅ |
| Portfolio risk parity | ⚠️ inverse-vol | ✅ equal-risk-contribution | ✅ equal-risk-contribution | ✅ equal-risk-contribution | ✅ equal-risk-contribution |
| Black-Litterman | ✅ | ✅ | ✅ | ✅ | ✅ |
| Screener | ⚠️ hardcoded provider | ⚠️ hardcoded provider | ⚠️ hardcoded provider | ⚠️ hardcoded provider | ⚠️ hardcoded provider |
| Hash-chained audit | ✅ | ✅ | ✅ | ✅ | ✅ |
| Agent tool dispatcher | ✅ | ✅ | ✅ (19 tools) | ✅ (42 tools) | ✅ (11 tools) |

## Security & audit

| Feature | C# | Kotlin | Go | Rust | C++ |
|---|---|---|---|---|---|
| API-key auth on REST | ✅ fail-closed | ✅ fail-closed | ✅ fail-closed | ✅ fail-closed | ✅ fail-closed |
| API-key auth on gRPC | N/A | ✅ | ✅ | ✅ | ❌ |
| TLS termination | ❌ | ❌ | ❌ | ❌ | ❌ |
| Audit provenance (git commit / version / seed) | ⚠️ schema only | ⚠️ commit + version | ✅ all three | ❌ none recorded | ✅ all three |
| Replay read-only / side-effect blocklist | ❌ not implemented | ✅ blocklist | ❌ re-executes | ⚠️ blocklist, CLI placeholder | ⚠️ read-only fetch, no re-execution |
| Persistent audit storage | ✅ SQLite + memory | ✅ PostgreSQL | ✅ PostgreSQL + memory | ✅ PostgreSQL + memory | ✅ PostgreSQL + memory |

## Operational hardening

| Feature | C# | Kotlin | Go | Rust | C++ |
|---|---|---|---|---|---|
| Request body limit | 16 MiB | 16 MB + 4 MB gRPC | 16 MiB | 16 MiB | 16 MiB |
| HTTP/gRPC request timeout | configured | 30 s netty | configured | 60 s | ❌ |
| Backtest bar cap | 50 000 | 50 000 | 50 000 | 50 000 | 50 000 |
| Monte Carlo simulation cap | 100 000 | 100 000 / 2 520 horizon | 100 000 | 10 000 | 100 000 |
| Walk-forward window cap | 10 000 | 10 000 | 10 000 | 10 000 | 10 000 |
| Walk-forward combination cap | 10 000 | 10 000 | 10 000 | 10 000 | 10 000 |
| Portfolio asset cap | 100 | 100 | 100 | 100 | 100 |
| Screener ticker cap | 500 | 500 | 500 | 100 | 500 |
| Structured logging / request tracing | ❌ | ❌ | ❌ | ❌ | ❌ |
| Metrics / Prometheus endpoint | ❌ | ✅ | ❌ | ❌ | ❌ |

## CI status

Validation below was performed locally with `nektos/act` on `linux/arm64` (Podman) using the workflow job(s) that exercise the core build and tests.

| Port | Status | Notes |
|---|---|---|
| C# | ✅ green | `act push --job build-and-test` passes (`dotnet test` 88 tests) |
| Kotlin | ✅ green | `act push --job unit-tests` passes; native build not validated locally |
| Go | ✅ green | `act push --job quality` passes |
| Rust | ✅ green | `act push --job test` passes; artifact upload skipped under `env.ACT` |
| C++ | ✅ green | `act push --job quality` passes (build + ctest)

## Known limitations relevant to this port

- No gRPC, A2A, MCP, Docker, or CLI surfaces.
- Market-data endpoint echoes parsed parameters rather than fetching live data.
- Audit provenance fields (`git_commit_sha`, `package_version`, `random_seed`) are defined but not populated by the API.
- Risk-parity weights are inverse-volatility, not equal-risk-contribution.
- PCA uses power iteration with fixed iterations and no convergence check.

## Outstanding P0/P1 gaps (deferred)

The following items were identified in the staff-engine audit and are explicitly documented rather than hidden behind false claims:

1. **TLS termination** — not implemented in any port. Deploy behind a reverse proxy that terminates TLS.
2. **Structured logging / request tracing** — no request-id propagation or structured log output.
3. **gRPC, A2A, MCP, Docker surfaces** — this port exposes REST only; no container image or alternate transports exist yet.
4. **Audit replay side-effect blocklist** — replay loads the record but does not guard against re-execution of side-effecting tools.
5. **Risk parity** — the implementation is inverse-volatility, not equal-risk-contribution; rename or replace before claiming parity.
6. **Dependency scanning** — no Dependabot or equivalent vulnerability scanning is wired into CI.

## Recommendations before a release tag

1. Populate audit provenance fields on every record.
2. Add Docker / container image support and a non-root `Dockerfile`.
3. Either implement gRPC/A2A/MCP or keep the claims limited to REST.
4. Replace inverse-volatility risk parity with a true risk-budget algorithm or rename the function.
