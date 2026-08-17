using StandardTools.Screener;
using Xunit;

namespace StandardTools.Screener.Tests;

public class ScreenerTests
{
    private static readonly FundamentalData Aapl = new()
    {
        Ticker = "AAPL",
        MarketCap = 3_000_000_000_000,
        PERatio = 28,
        PBRatio = 45,
        DividendYield = 0.005,
        EPSGrowth = 0.12,
        DebtToEquity = 1.5,
        ROE = 0.30
    };

    private static readonly FundamentalData Msft = new()
    {
        Ticker = "MSFT",
        MarketCap = 2_500_000_000_000,
        PERatio = 32,
        PBRatio = 12,
        DividendYield = 0.007,
        EPSGrowth = 0.15,
        DebtToEquity = 0.5,
        ROE = 0.40
    };

    private static readonly FundamentalData Xyz = new()
    {
        Ticker = "XYZ",
        MarketCap = 1_000_000_000,
        PERatio = double.NaN,
        PBRatio = 2,
        DividendYield = 0.0,
        EPSGrowth = -0.05,
        DebtToEquity = 0.2,
        ROE = 0.05
    };

    [Fact]
    public async Task Screen_AppliesCriteria()
    {
        var provider = new HardcodedProvider(new Dictionary<string, FundamentalData>
        {
            ["AAPL"] = Aapl,
            ["MSFT"] = Msft,
            ["XYZ"] = Xyz
        });
        var screener = new Screener(provider);

        var result = await screener.ScreenAsync(["AAPL", "MSFT", "XYZ"], new ScreenCriteria
        {
            PERatioMax = 35,
            ROEMin = 0.35,
            DebtToEquityMax = 1.0
        });

        Assert.Single(result.Matches);
        Assert.Equal("MSFT", result.Matches[0].Ticker);
        Assert.Equal(2, result.Failed.Count);
        Assert.Contains("AAPL", result.Failed);
        Assert.Contains("XYZ", result.Failed);
    }

    [Fact]
    public async Task Screen_MissingTicker_IsFailed()
    {
        var provider = new HardcodedProvider(new Dictionary<string, FundamentalData> { ["AAPL"] = Aapl });
        var screener = new Screener(provider);

        var result = await screener.ScreenAsync(["AAPL", "UNKNOWN"], new ScreenCriteria());

        Assert.Single(result.Matches);
        Assert.Single(result.Failed);
        Assert.Equal("UNKNOWN", result.Failed[0]);
    }

    [Fact]
    public async Task Screen_NonFiniteValue_Fails()
    {
        var provider = new HardcodedProvider(new Dictionary<string, FundamentalData> { ["XYZ"] = Xyz });
        var screener = new Screener(provider);

        var result = await screener.ScreenAsync(["XYZ"], new ScreenCriteria { PERatioMax = 100 });

        Assert.Empty(result.Matches);
        Assert.Single(result.Failed);
    }

    [Fact]
    public async Task Screen_NoCriteria_MatchesAll()
    {
        var provider = new HardcodedProvider(new Dictionary<string, FundamentalData>
        {
            ["AAPL"] = Aapl,
            ["MSFT"] = Msft
        });
        var screener = new Screener(provider);

        var result = await screener.ScreenAsync(["AAPL", "MSFT"], new ScreenCriteria());

        Assert.Equal(2, result.Matches.Count);
        Assert.Empty(result.Failed);
    }

    private sealed class HardcodedProvider : IFundamentalProvider
    {
        private readonly IReadOnlyDictionary<string, FundamentalData> _data;

        public HardcodedProvider(IReadOnlyDictionary<string, FundamentalData> data) => _data = data;

        public Task<FundamentalData?> FetchAsync(string ticker, CancellationToken cancellationToken = default) =>
            Task.FromResult(_data.TryGetValue(ticker, out var data) ? data : null);
    }
}
