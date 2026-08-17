namespace StandardTools.Screener;

/// <summary>
/// Provides fundamental data for a single ticker.
/// </summary>
public interface IFundamentalProvider
{
    /// <summary>
    /// Fetches fundamental data for the given ticker.
    /// </summary>
    /// <param name="ticker">The ticker symbol.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The fundamental data, or null if not available.</returns>
    Task<FundamentalData?> FetchAsync(string ticker, CancellationToken cancellationToken = default);
}
