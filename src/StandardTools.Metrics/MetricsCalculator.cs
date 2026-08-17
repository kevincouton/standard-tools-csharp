namespace StandardTools.Metrics;

/// <summary>
/// Computes risk and return metrics from a series of close prices.
/// </summary>
public sealed class MetricsCalculator
{
    public const double DefaultRiskFreeRate = 0.02;
    public const int TradingDaysPerYear = 252;

    public double RiskFreeRate { get; }

    public MetricsCalculator(double riskFreeRate = DefaultRiskFreeRate)
    {
        RiskFreeRate = riskFreeRate;
    }

    /// <summary>
    /// Calculates return and risk metrics from a slice of close prices.
    /// </summary>
    /// <param name="closes">Positive, finite close prices. At least two observations are required.</param>
    /// <returns>Cumulative/annualized return metrics and risk metrics.</returns>
    public (ReturnMetrics Returns, RiskMetrics Risk) Calculate(IReadOnlyList<double> closes)
    {
        if (closes.Count < 2)
        {
            throw new InsufficientDataException($"at least two close prices required, got {closes.Count}");
        }

        for (var i = 0; i < closes.Count; i++)
        {
            var p = closes[i];
            if (p <= 0 || double.IsNaN(p) || double.IsInfinity(p))
            {
                throw new InvalidPricesException($"price at index {i} must be positive and finite (got {p})");
            }
        }

        var returns = new double[closes.Count - 1];
        for (var i = 1; i < closes.Count; i++)
        {
            returns[i - 1] = closes[i] / closes[i - 1] - 1.0;
        }

        var cumulative = CumulativeReturn(returns);
        var periods = (double)returns.Length;
        var cagr = Math.Pow(1.0 + cumulative, TradingDaysPerYear / periods) - 1.0;
        var vol = AnnualizedVolatility(returns);
        var maxDD = MaxDrawdown(returns);

        var returnMetrics = new ReturnMetrics
        {
            CumulativeReturn = cumulative,
            Cagr = cagr,
            AnnualizedVolatility = vol
        };

        var riskMetrics = new RiskMetrics
        {
            SharpeRatio = SharpeRatio(cagr, vol, RiskFreeRate),
            SortinoRatio = SortinoRatio(returns, cagr, RiskFreeRate),
            MaxDrawdown = maxDD,
            CalmarRatio = CalmarRatio(cagr, maxDD),
            VaR95 = HistoricalVaR(returns, 0.05),
            CVaR95 = HistoricalCVaR(returns, 0.05),
            Volatility = vol
        };

        return (returnMetrics, riskMetrics);
    }

    private static double CumulativeReturn(IReadOnlyList<double> returns)
    {
        var product = 1.0;
        foreach (var r in returns)
        {
            product *= 1.0 + r;
        }
        return product - 1.0;
    }

    private static double Mean(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return double.NaN;
        }
        return values.Sum() / values.Count;
    }

    private static double AnnualizedVolatility(IReadOnlyList<double> returns)
    {
        if (returns.Count < 2)
        {
            return 0.0;
        }

        var m = Mean(returns);
        var sumSq = returns.Sum(r =>
        {
            var d = r - m;
            return d * d;
        });
        var variance = sumSq / returns.Count;
        return Math.Sqrt(variance) * Math.Sqrt(TradingDaysPerYear);
    }

    private static double SharpeRatio(double cagr, double volatility, double riskFreeRate)
    {
        if (volatility <= 0 || double.IsNaN(volatility))
        {
            return double.NaN;
        }
        return (cagr - riskFreeRate) / volatility;
    }

    private static double SortinoRatio(IReadOnlyList<double> returns, double cagr, double riskFreeRate)
    {
        if (returns.Count == 0)
        {
            return double.NaN;
        }

        var periodicRf = riskFreeRate / TradingDaysPerYear;
        var downsideSum = 0.0;
        foreach (var r in returns)
        {
            var d = r - periodicRf;
            if (d < 0)
            {
                downsideSum += d * d;
            }
        }

        var downsideDeviation = Math.Sqrt(downsideSum / returns.Count) * Math.Sqrt(TradingDaysPerYear);
        if (downsideDeviation <= 0 || double.IsNaN(downsideDeviation))
        {
            return double.NaN;
        }
        return (cagr - riskFreeRate) / downsideDeviation;
    }

    private static double MaxDrawdown(IReadOnlyList<double> returns)
    {
        var equity = 1.0;
        var peak = 1.0;
        var maxDD = 0.0;
        foreach (var r in returns)
        {
            equity *= 1.0 + r;
            if (equity > peak)
            {
                peak = equity;
            }

            var drawdown = (peak - equity) / peak;
            if (drawdown > maxDD)
            {
                maxDD = drawdown;
            }
        }
        return -maxDD;
    }

    private static double HistoricalVaR(IReadOnlyList<double> returns, double quantile)
    {
        if (returns.Count == 0)
        {
            return double.NaN;
        }

        var sorted = returns.OrderBy(r => r).ToArray();
        var idx = (int)Math.Round(quantile * (sorted.Length - 1));
        idx = Math.Clamp(idx, 0, sorted.Length - 1);
        return sorted[idx];
    }

    private static double HistoricalCVaR(IReadOnlyList<double> returns, double quantile)
    {
        var varValue = HistoricalVaR(returns, quantile);
        if (double.IsNaN(varValue))
        {
            return double.NaN;
        }

        var sum = 0.0;
        var count = 0;
        foreach (var r in returns)
        {
            if (r <= varValue)
            {
                sum += r;
                count++;
            }
        }

        if (count == 0)
        {
            return double.NaN;
        }
        return sum / count;
    }

    private static double CalmarRatio(double cagr, double maxDrawdown)
    {
        var dd = Math.Abs(maxDrawdown);
        if (dd <= 0)
        {
            return double.NaN;
        }
        return cagr / dd;
    }
}
