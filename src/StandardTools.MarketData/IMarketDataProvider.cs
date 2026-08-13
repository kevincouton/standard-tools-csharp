using StandardTools.Core;

namespace StandardTools.MarketData;

public interface IMarketDataProvider
{
    string Name { get; }

    Task<IReadOnlyList<OHLCV>> FetchAsync(
        Ticker ticker,
        DateRange range,
        BarInterval interval,
        CancellationToken cancellationToken = default);
}
