using System.Text.Json;
using StandardTools.Api;
using Xunit;

namespace StandardTools.Api.Tests;

public class HardcodedFundamentalProviderTests
{
    [Fact]
    public async Task ParsesConfig()
    {
        var json = JsonSerializer.Serialize(new
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
        });
        var config = JsonDocument.Parse(json).RootElement;
        var provider = new HardcodedFundamentalProvider(config);
        var data = await provider.FetchAsync("AAPL");
        Assert.NotNull(data);
        Assert.Equal(20, data.PERatio);
    }
}
