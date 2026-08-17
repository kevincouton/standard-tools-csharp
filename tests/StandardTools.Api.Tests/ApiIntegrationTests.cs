using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StandardTools.Api.Tests;

// Env vars (SQT_AUTH_ENABLED / SQT_API_KEY) are process-wide, so all
// WebApplicationFactory-based tests share one non-parallelized collection.
[CollectionDefinition("Api", DisableParallelization = true)]
public sealed class ApiCollectionDefinition;

[Collection("Api")]
public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        // These tests exercise endpoints, not auth; disable it before the host is built.
        Environment.SetEnvironmentVariable("SQT_AUTH_ENABLED", "false");
        _factory = factory;
    }

    private HttpClient Client => _factory.CreateClient();

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await Client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", content.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetTools_ReturnsList()
    {
        var response = await Client.GetAsync("/api/v1/agent/tools");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(content.GetArrayLength() > 0);
    }

    [Fact]
    public async Task PostIndicators_Sma()
    {
        var request = new
        {
            dates = new[] { "2026-01-01", "2026-01-02", "2026-01-03", "2026-01-04", "2026-01-05" },
            opens = new[] { 100m, 101m, 102m, 103m, 104m },
            highs = new[] { 100m, 101m, 102m, 103m, 104m },
            lows = new[] { 100m, 101m, 102m, 103m, 104m },
            closes = new[] { 100m, 101m, 102m, 103m, 104m },
            volumes = new[] { 1000L, 1000L, 1000L, 1000L, 1000L },
            param = new Dictionary<string, string> { ["period"] = "3" }
        };
        var response = await Client.PostAsJsonAsync("/api/v1/indicators/sma", request);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("sma", content.GetProperty("name").GetString());
    }

    [Fact]
    public async Task PostMetricsRisk_ReturnsSharpe()
    {
        var request = new { values = new[] { 100.0, 101.0, 102.0, 101.0, 103.0 } };
        var response = await Client.PostAsJsonAsync("/api/v1/metrics/risk", request);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(content.GetProperty("sharpe_ratio").ValueKind == JsonValueKind.Number);
    }

    [Fact]
    public async Task PostBacktest_BuyAndHold()
    {
        var request = new
        {
            dates = new[] { "2026-01-01", "2026-01-02", "2026-01-03", "2026-01-04", "2026-01-05" },
            closes = new[] { 100m, 101m, 102m, 103m, 104m }
        };
        var response = await Client.PostAsJsonAsync("/api/v1/backtest/buy_and_hold", request);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(content.GetProperty("metrics").GetProperty("tradeCount").ValueKind == JsonValueKind.Number);
    }

    [Fact]
    public async Task PostPortfolioOptimize()
    {
        var request = new
        {
            returns = new[] { new[] { 0.01, 0.02, -0.01 }, new[] { 0.005, 0.015, 0.0 } },
            labels = new[] { "A", "B" },
            objective = "max_sharpe"
        };
        var response = await Client.PostAsJsonAsync("/api/v1/portfolio/optimize", request);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(content.GetProperty("expectedReturn").ValueKind == JsonValueKind.Number);
    }

    [Fact]
    public async Task PostScreener()
    {
        var request = new
        {
            tickers = new[] { "AAPL" },
            criteria = new { pe_ratio_max = 25.0 },
            provider_type = "hardcoded",
            provider_config = new
            {
                AAPL = new
                {
                    market_cap = 3e12,
                    pe_ratio = 20,
                    pb_ratio = 5,
                    dividend_yield = 0.01,
                    eps_growth = 0.1,
                    debt_to_equity = 0.5,
                    roe = 0.3
                }
            }
        };
        var response = await Client.PostAsJsonAsync("/api/v1/screener", request);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(content.GetProperty("failed").EnumerateArray());
        Assert.Single(content.GetProperty("matches").EnumerateArray());
    }

    [Fact]
    public async Task PostAgentTool()
    {
        var request = new
        {
            name = "health",
            arguments = new { }
        };
        var response = await Client.PostAsJsonAsync("/api/v1/agent/tools", request);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", content.GetProperty("status").GetString());
    }

    [Fact]
    public async Task AuditFlow_RecordVerifyReplay()
    {
        var record = new
        {
            request_id = "r1",
            tool_name = "list_tools",
            input = new { },
            input_hash = "",
            output = new { tools = new[] { "list_tools" } },
            output_hash = "",
            status = "ok",
            record_hash = ""
        };

        var writeResponse = await Client.PostAsJsonAsync("/api/v1/audit/record", record);
        var writeBody = await writeResponse.Content.ReadAsStringAsync();
        Assert.True(writeResponse.IsSuccessStatusCode, $"audit write failed: {writeBody}");

        var verifyResponse = await Client.PostAsJsonAsync("/api/v1/audit/verify", new { });
        var verifyBody = await verifyResponse.Content.ReadAsStringAsync();
        Assert.True(verifyResponse.IsSuccessStatusCode, $"audit verify failed: {verifyBody}");

        var replayResponse = await Client.GetAsync("/api/v1/audit/replay/r1");
        replayResponse.EnsureSuccessStatusCode();
        var content = await replayResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("r1", content.GetProperty("request_id").GetString());
    }
}
