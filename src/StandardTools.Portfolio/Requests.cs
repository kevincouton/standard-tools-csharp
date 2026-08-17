namespace StandardTools.Portfolio;

public static class PortfolioObjective
{
    public const string MaxSharpe = "max_sharpe";
    public const string MinVolatility = "min_volatility";
    public const string TargetReturn = "target_return";
    public const string TargetVolatility = "target_volatility";
}

public sealed class MeanVarianceRequest
{
    public required IReadOnlyList<IReadOnlyList<double>> Returns { get; init; }
    public required IReadOnlyList<string> Labels { get; init; }
    public double RiskFreeRate { get; init; } = 0.0;
    public required string Objective { get; init; }
    public double? TargetReturn { get; init; }
    public double? TargetVolatility { get; init; }
}

public sealed class RiskParityRequest
{
    public required IReadOnlyList<IReadOnlyList<double>> Returns { get; init; }
    public required IReadOnlyList<string> Labels { get; init; }
}

public sealed class BlackLittermanRequest
{
    public required IReadOnlyList<IReadOnlyList<double>> Returns { get; init; }
    public required IReadOnlyList<string> Labels { get; init; }
    public required IReadOnlyList<double> MarketCaps { get; init; }
    public required IReadOnlyList<IReadOnlyList<double>> PMatrix { get; init; }
    public required IReadOnlyList<double> QVector { get; init; }
    public double Tau { get; init; } = 0.05;
    public double RiskAversion { get; init; } = 2.5;
}

public sealed class BlackLittermanSimplifiedRequest
{
    public required IReadOnlyList<IReadOnlyList<double>> Returns { get; init; }
    public required IReadOnlyList<string> Labels { get; init; }
    public required IReadOnlyDictionary<string, double> MarketCaps { get; init; }
    public required IReadOnlyDictionary<string, double> Views { get; init; }
    public double Tau { get; init; } = 0.05;
    public double RiskAversion { get; init; } = 2.5;
}
