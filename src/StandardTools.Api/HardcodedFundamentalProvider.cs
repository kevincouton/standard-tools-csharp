using System.Text.Json;
using StandardTools.Core;
using StandardTools.Screener;

namespace StandardTools.Api;

/// <summary>
/// Hardcoded fundamental provider for demo purposes. Configuration is supplied as JSON.
/// </summary>
public sealed class HardcodedFundamentalProvider : IFundamentalProvider
{
    private readonly IReadOnlyDictionary<string, FundamentalData> _data;

    public HardcodedFundamentalProvider(JsonElement? config)
    {
        var dictionary = new Dictionary<string, FundamentalData>(StringComparer.OrdinalIgnoreCase);
        if (config is { ValueKind: JsonValueKind.Object } obj)
        {
            foreach (var property in obj.EnumerateObject())
            {
                if (TryParseData(property.Value, property.Name) is { } data)
                    dictionary[property.Name] = data;
            }
        }
        _data = dictionary;
    }

    public Task<FundamentalData?> FetchAsync(string ticker, CancellationToken cancellationToken = default) =>
        Task.FromResult(_data.TryGetValue(ticker, out var data) ? data : null);

    private static FundamentalData? TryParseData(JsonElement element, string ticker)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        try
        {
            return new FundamentalData
            {
                Ticker = ticker,
                MarketCap = GetDoubleOrDefault(element, "market_cap"),
                PERatio = GetDoubleOrDefault(element, "pe_ratio"),
                PBRatio = GetDoubleOrDefault(element, "pb_ratio"),
                DividendYield = GetDoubleOrDefault(element, "dividend_yield"),
                EPSGrowth = GetDoubleOrDefault(element, "eps_growth"),
                DebtToEquity = GetDoubleOrDefault(element, "debt_to_equity"),
                ROE = GetDoubleOrDefault(element, "roe")
            };
        }
        catch (Exception ex)
        {
            throw new InvalidCommandException($"failed to parse fundamental data for {ticker}: {ex.Message}");
        }
    }

    private static double GetDoubleOrDefault(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var propertyElement))
            throw new InvalidCommandException($"missing property {property}");

        return propertyElement.ValueKind switch
        {
            JsonValueKind.Number => propertyElement.GetDouble(),
            JsonValueKind.String => double.Parse(propertyElement.GetString()!, System.Globalization.CultureInfo.InvariantCulture),
            _ => throw new InvalidCommandException($"property {property} must be a number")
        };
    }
}
