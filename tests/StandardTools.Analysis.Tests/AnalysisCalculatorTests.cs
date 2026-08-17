using Xunit;

namespace StandardTools.Analysis.Tests;

public class AnalysisCalculatorTests
{
    [Fact]
    public void LinearRegression_KnownLine()
    {
        var xs = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
        var ys = xs.Select(x => 2 * x + 1).ToArray();
        var calc = new AnalysisCalculator();
        var result = calc.LinearRegression(xs, ys);

        Assert.Equal(2.0, result.Slope, 6);
        Assert.Equal(1.0, result.Intercept, 6);
        Assert.Equal(1.0, result.RSquared, 6);
    }

    [Fact]
    public void Correlation_PerfectPositive()
    {
        var xs = Enumerable.Range(1, 20).Select(i => (double)i).ToArray();
        var ys = xs.Select(x => x * 3 + 5).ToArray();
        var result = new AnalysisCalculator().Correlation(xs, ys);
        Assert.Equal(1.0, result.Pearson, 6);
    }

    [Fact]
    public void Correlation_PerfectNegative()
    {
        var xs = Enumerable.Range(1, 20).Select(i => (double)i).ToArray();
        var ys = xs.Select(x => -2 * x + 10).ToArray();
        var result = new AnalysisCalculator().Correlation(xs, ys);
        Assert.Equal(-1.0, result.Pearson, 6);
    }

    [Fact]
    public void Cointegration_CointegratedSeries()
    {
        var random = new Random(42);
        var x = new double[50];
        x[0] = 100;
        for (var i = 1; i < x.Length; i++)
            x[i] = x[i - 1] + random.NextDouble() - 0.5;

        var y = x.Select((xi, i) => 0.5 * xi + 2 + (random.NextDouble() - 0.5) * 0.5).ToArray();
        var result = new AnalysisCalculator().Cointegration(x, y);
        Assert.Equal(0.5, result.HedgeRatio, 1);
        Assert.True(result.ResidualHurst >= 0 && result.ResidualHurst <= 1);
    }

    [Fact]
    public void Hurst_RandomWalk_IsBetweenZeroAndOne()
    {
        var random = new Random(42);
        var values = new double[200];
        values[0] = 100;
        for (var i = 1; i < values.Length; i++)
            values[i] = values[i - 1] + (random.NextDouble() - 0.5);

        var result = new AnalysisCalculator().Hurst(values);
        Assert.True(result.HurstExponent > 0 && result.HurstExponent < 1);
    }

    [Fact]
    public void Pca_FirstComponentCapturesMostVariance()
    {
        var series = new List<IReadOnlyList<double>>
        {
            Enumerable.Range(1, 30).Select(i => (double)i + i * 0.1).ToArray(),
            Enumerable.Range(1, 30).Select(i => (double)i * 2 + i * 0.05).ToArray()
        };

        var result = new AnalysisCalculator().Pca(series, 2);
        Assert.Equal(2, result.Eigenvalues.Count);
        Assert.True(result.Eigenvalues[0] >= result.Eigenvalues[1]);
        Assert.True(result.ExplainedVarianceRatios[0] > result.ExplainedVarianceRatios[1]);
    }

    [Fact]
    public void MultiFactor_RecoversBetas()
    {
        var random = new Random(123);
        var factor1 = Enumerable.Range(0, 50).Select(_ => random.NextDouble() - 0.5).ToArray();
        var factor2 = Enumerable.Range(0, 50).Select(_ => random.NextDouble() - 0.5).ToArray();
        var asset = factor1.Select((f1, i) => 0.01 + 0.5 * f1 + 0.3 * factor2[i]).ToArray();

        var result = new AnalysisCalculator().MultiFactor(asset, new[] { factor1, factor2 });
        Assert.Equal(0.01, result.Alpha, 2);
        Assert.Equal(0.5, result.Betas[0], 1);
        Assert.Equal(0.3, result.Betas[1], 1);
        Assert.True(result.RSquared > 0.8);
    }

    [Fact]
    public void Options_BlackScholes_CallPriceBounds()
    {
        var result = new AnalysisCalculator().Options("call", 100, 100, 0.05, 0.2, 1.0);
        Assert.True(result.Price > 0 && result.Price < 100);
        Assert.True(result.Delta > 0 && result.Delta < 1);
        Assert.True(result.Gamma > 0);
        Assert.True(result.Vega > 0);
    }

    [Fact]
    public void Options_PutPrice_Positive()
    {
        var result = new AnalysisCalculator().Options("put", 100, 100, 0.05, 0.2, 1.0);
        Assert.True(result.Price > 0);
        Assert.True(result.Delta > -1 && result.Delta < 0);
    }
}
