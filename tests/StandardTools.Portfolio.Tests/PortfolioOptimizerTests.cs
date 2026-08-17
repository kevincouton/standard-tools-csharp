using StandardTools.Core;
using Xunit;

namespace StandardTools.Portfolio.Tests;

public class PortfolioOptimizerTests
{
    private static (IReadOnlyList<IReadOnlyList<double>> Returns, IReadOnlyList<string> Labels) SyntheticTwoAssetReturns() =>
    (new[]
    {
        new[] { 0.01, 0.02, -0.01, 0.015, 0.005, 0.025, -0.005, 0.01, 0.0, 0.02 },
        new[] { 0.005, 0.01, 0.0, 0.008, 0.004, 0.012, 0.002, 0.006, 0.003, 0.009 }
    }, new[] { "A", "B" });

    private static (IReadOnlyList<IReadOnlyList<double>> Returns, IReadOnlyList<string> Labels) SyntheticThreeAssetReturns() =>
    (new[]
    {
        new[] { 0.01, 0.02, -0.01, 0.015, 0.005, 0.025, -0.005, 0.01, 0.0, 0.02, 0.012, 0.018 },
        new[] { 0.005, 0.01, 0.0, 0.008, 0.004, 0.012, 0.002, 0.006, 0.003, 0.009, 0.007, 0.011 },
        new[] { 0.0, 0.005, 0.002, 0.003, 0.001, 0.008, 0.004, 0.002, 0.005, 0.006, 0.001, 0.004 }
    }, new[] { "A", "B", "C" });

    [Fact]
    public void MeanVariance_MaxSharpe()
    {
        var (returns, labels) = SyntheticTwoAssetReturns();
        var result = PortfolioOptimizer.MeanVariance(new MeanVarianceRequest
        {
            Returns = returns,
            Labels = labels,
            Objective = PortfolioObjective.MaxSharpe
        });

        result.Validate();
        Assert.Equal(1.0, result.Weights.Values.Sum(), 6);
        Assert.True(result.ExpectedReturn > 0);
        Assert.True(result.Volatility > 0);
    }

    [Fact]
    public void MeanVariance_MinVolatility()
    {
        var (returns, labels) = SyntheticTwoAssetReturns();
        var result = PortfolioOptimizer.MeanVariance(new MeanVarianceRequest
        {
            Returns = returns,
            Labels = labels,
            Objective = PortfolioObjective.MinVolatility
        });

        result.Validate();
        Assert.True(result.Weights["B"] > result.Weights["A"]);
    }

    [Fact]
    public void MeanVariance_TargetReturn()
    {
        var (returns, labels) = SyntheticTwoAssetReturns();
        var minVol = PortfolioOptimizer.MeanVariance(new MeanVarianceRequest { Returns = returns, Labels = labels, Objective = PortfolioObjective.MinVolatility });
        var maxSharpe = PortfolioOptimizer.MeanVariance(new MeanVarianceRequest { Returns = returns, Labels = labels, Objective = PortfolioObjective.MaxSharpe });
        var target = (minVol.ExpectedReturn + maxSharpe.ExpectedReturn) / 2;

        var result = PortfolioOptimizer.MeanVariance(new MeanVarianceRequest
        {
            Returns = returns,
            Labels = labels,
            Objective = PortfolioObjective.TargetReturn,
            TargetReturn = target
        });

        result.Validate();
        Assert.Equal(target, result.ExpectedReturn, 6);
    }

    [Fact]
    public void MeanVariance_TargetVolatility()
    {
        var (returns, labels) = SyntheticTwoAssetReturns();
        var minVol = PortfolioOptimizer.MeanVariance(new MeanVarianceRequest { Returns = returns, Labels = labels, Objective = PortfolioObjective.MinVolatility });
        var maxSharpe = PortfolioOptimizer.MeanVariance(new MeanVarianceRequest { Returns = returns, Labels = labels, Objective = PortfolioObjective.MaxSharpe });
        var target = (minVol.Volatility + maxSharpe.Volatility) / 2;

        var result = PortfolioOptimizer.MeanVariance(new MeanVarianceRequest
        {
            Returns = returns,
            Labels = labels,
            Objective = PortfolioObjective.TargetVolatility,
            TargetVolatility = target
        });

        result.Validate();
        Assert.Equal(target, result.Volatility, 4);
    }

    [Fact]
    public void MeanVariance_RiskFreeRate()
    {
        var (returns, labels) = SyntheticTwoAssetReturns();
        var result = PortfolioOptimizer.MeanVariance(new MeanVarianceRequest
        {
            Returns = returns,
            Labels = labels,
            Objective = PortfolioObjective.MaxSharpe,
            RiskFreeRate = 0.005
        });

        var wantSharpe = (result.ExpectedReturn - 0.005) / result.Volatility;
        Assert.Equal(wantSharpe, result.SharpeRatio, 6);
    }

    [Fact]
    public void MeanVariance_ValidationErrors()
    {
        Assert.Throws<DataQualityException>(() => PortfolioOptimizer.MeanVariance(new MeanVarianceRequest
        {
            Returns = new List<IReadOnlyList<double>>(),
            Labels = new List<string>(),
            Objective = PortfolioObjective.MaxSharpe
        }));

        Assert.Throws<InvalidCommandException>(() => PortfolioOptimizer.MeanVariance(new MeanVarianceRequest
        {
            Returns = new[] { new[] { 0.01, 0.02 }, new[] { 0.005, 0.01 } },
            Labels = new[] { "A" },
            Objective = PortfolioObjective.MaxSharpe
        }));

        Assert.Throws<DataQualityException>(() => PortfolioOptimizer.MeanVariance(new MeanVarianceRequest
        {
            Returns = new[] { new[] { 0.01, double.NaN }, new[] { 0.005, 0.01 } },
            Labels = new[] { "A", "B" },
            Objective = PortfolioObjective.MaxSharpe
        }));

        Assert.Throws<InvalidCommandException>(() => PortfolioOptimizer.MeanVariance(new MeanVarianceRequest
        {
            Returns = new[] { new[] { 0.01, 0.02 }, new[] { 0.005, 0.01 } },
            Labels = new[] { "A", "B" },
            Objective = PortfolioObjective.TargetReturn
        }));
    }

    [Fact]
    public void RiskParity_Basic()
    {
        var (returns, labels) = SyntheticTwoAssetReturns();
        var result = PortfolioOptimizer.RiskParity(new RiskParityRequest { Returns = returns, Labels = labels });

        result.Validate();
        Assert.True(result.Weights["B"] > result.Weights["A"]);
        Assert.Equal(1.0, result.Weights.Values.Sum(), 6);
    }

    [Fact]
    public void RiskParity_AllZeroVolatility()
    {
        Assert.Throws<DataQualityException>(() => PortfolioOptimizer.RiskParity(new RiskParityRequest
        {
            Returns = new[] { new[] { 0.01, 0.01, 0.01 }, new[] { 0.02, 0.02, 0.02 } },
            Labels = new[] { "A", "B" }
        }));
    }

    [Fact]
    public void BlackLittermanSimplified_Basic()
    {
        var (returns, labels) = SyntheticThreeAssetReturns();
        var (result, expected, cov) = PortfolioOptimizer.BlackLittermanSimplified(new BlackLittermanSimplifiedRequest
        {
            Returns = returns,
            Labels = labels,
            MarketCaps = new Dictionary<string, double> { ["A"] = 1000, ["B"] = 500, ["C"] = 200 },
            Views = new Dictionary<string, double> { ["A"] = 0.015, ["B"] = 0.008 }
        });

        result.Validate();
        Assert.Equal(labels.Count, expected.Count);
        Assert.Equal(labels.Count, cov.Length);
        Assert.Equal(labels.Count, cov[0].Length);
        Assert.Equal(1.0, result.Weights.Values.Sum(), 6);
    }

    [Fact]
    public void BlackLitterman_ExplicitViews()
    {
        var (returns, labels) = SyntheticThreeAssetReturns();
        var (result, expected, _) = PortfolioOptimizer.BlackLitterman(new BlackLittermanRequest
        {
            Returns = returns,
            Labels = labels,
            MarketCaps = new[] { 1000.0, 500.0, 200.0 },
            PMatrix = new[] { new[] { 1.0, 0.0, 0.0 }, new[] { 0.0, 1.0, -1.0 } },
            QVector = new[] { 0.015, 0.002 }
        });

        result.Validate();
        Assert.Equal(labels.Count, expected.Count);
    }

    [Fact]
    public void Result_Validate()
    {
        var valid = new PortfolioResult
        {
            Weights = new Dictionary<string, double> { ["A"] = 0.6, ["B"] = 0.4 },
            ExpectedReturn = 0.01,
            Volatility = 0.05,
            SharpeRatio = 0.2
        };
        valid.Validate();

        var badSum = valid with { Weights = new Dictionary<string, double> { ["A"] = 0.6, ["B"] = 0.3 } };
        Assert.Throws<InvalidCommandException>(() => badSum.Validate());

        var negativeVol = valid with { Volatility = -0.05 };
        Assert.Throws<DataQualityException>(() => negativeVol.Validate());
    }
}
