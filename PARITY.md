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
| Analysis (regression, cointegration, Hurst, PCA, correlation, options) | ✅ library; ⚠️ only regression + options exposed | ✅ | ⚠️ no multi-factor | ⚠️ no multi-factor | ⚠️ no multi-factor |
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
| Persistent audit storage | ❌ in-memory only | ✅ PostgreSQL | ✅ PostgreSQL + memory | ✅ PostgreSQL + memory | ✅ PostgreSQL + memory |

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

| Port | Status | Notes |
|---|---|---|
| C# | ✅ green | `dotnet test` passes (88 tests) |
| Kotlin | ✅ green | unit / integration / e2e green; native build not validated locally |
| Go | ✅ green | `go test ./...` and image builds green locally |
| Rust | ❌ red | `cargo fmt` not installed in mise toolchain; `set -o pipefail` fails under dash |
| C++ | ❌ red | `rm -rf /var/lib/apt/lists/*` lacks permissions in GitHub Actions runner |

## Known limitations relevant to this port

- No gRPC, A2A, MCP, Docker, or CLI surfaces.
- Market-data endpoint echoes parsed parameters rather than fetching live data.
- Audit storage is in-memory only; records do not survive process restart.
- Audit provenance fields (`git_commit_sha`, `package_version`, `random_seed`) are defined but not populated by the API.
- Risk-parity weights are inverse-volatility, not equal-risk-contribution.
- PCA uses power iteration with fixed iterations and no convergence check.

## Recommendations before a release tag

1. Implement a persistent audit backend (PostgreSQL or local SQLite).
2. Populate audit provenance fields on every record.
3. Add Docker / container image support and a non-root `Dockerfile`.
4. Either implement gRPC/A2A/MCP or remove the claims from downstream docs.
5. Replace inverse-volatility risk parity with a true risk-budget algorithm or rename the function.
