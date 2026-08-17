using StandardTools.Core;

namespace StandardTools.Portfolio;

public sealed record PortfolioResult
{
    public required IReadOnlyDictionary<string, double> Weights { get; init; }
    public required double ExpectedReturn { get; init; }
    public required double Volatility { get; init; }
    public required double SharpeRatio { get; init; }

    private const double WeightSumTolerance = 1e-6;

    public void Validate()
    {
        if (Weights.Count == 0)
            throw new DataQualityException("result contains no weights");

        var sum = Weights.Values.Sum();
        if (double.IsNaN(sum) || double.IsInfinity(sum) || Math.Abs(sum - 1.0) > WeightSumTolerance)
            throw new InvalidCommandException($"weights sum to {sum}, expected 1.0");

        foreach (var (label, w) in Weights)
        {
            if (double.IsNaN(w) || double.IsInfinity(w))
                throw new DataQualityException($"weight for {label} is non-finite");
        }

        if (double.IsNaN(ExpectedReturn) || double.IsInfinity(ExpectedReturn))
            throw new DataQualityException("expected return is non-finite");

        if (double.IsNaN(Volatility) || double.IsInfinity(Volatility) || Volatility < 0)
            throw new DataQualityException("volatility is non-finite or negative");

        if (double.IsNaN(SharpeRatio) || double.IsInfinity(SharpeRatio))
            throw new DataQualityException("Sharpe ratio is non-finite");
    }
}
