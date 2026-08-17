using StandardTools.Core;

namespace StandardTools.Screener;

/// <summary>
/// Filters a universe of tickers by fundamental criteria.
/// </summary>
public sealed class Screener
{
    public const int MaxTickers = 500;

    private readonly IFundamentalProvider _provider;

    public Screener(IFundamentalProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /// <summary>
    /// Fetches fundamental data for each ticker, applies the criteria, and returns the matches.
    /// Tickers that cannot be fetched or do not satisfy the criteria are recorded in <see cref="ScreenResult.Failed"/>.
    /// </summary>
    public async Task<ScreenResult> ScreenAsync(IReadOnlyList<string> tickers, ScreenCriteria criteria, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tickers);
        ArgumentNullException.ThrowIfNull(criteria);
        if (tickers.Count > MaxTickers)
            throw new InvalidCommandException($"screener supports at most {MaxTickers} tickers");

        var matches = new List<FundamentalData>();
        var failed = new List<string>();

        foreach (var ticker in tickers)
        {
            var data = await _provider.FetchAsync(ticker, cancellationToken).ConfigureAwait(false);
            if (data is null)
            {
                failed.Add(ticker);
                continue;
            }

            if (criteria.Apply(data))
            {
                matches.Add(data);
            }
            else
            {
                failed.Add(ticker);
            }
        }

        return new ScreenResult
        {
            Matches = matches,
            Failed = failed
        };
    }
}
