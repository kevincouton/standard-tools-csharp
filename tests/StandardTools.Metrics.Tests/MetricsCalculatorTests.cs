using System.Text.Json;
using Xunit;

namespace StandardTools.Metrics.Tests;

public class MetricsCalculatorTests
{
    private const double Epsilon = 1e-9;

    private static bool ApproxEqual(double a, double b) =>
        (double.IsNaN(a) && double.IsNaN(b)) || Math.Abs(a - b) <= Epsilon;

    [Fact]
    public void KnownSeries()
    {
        var closes = new[] { 100.0, 110.0, 105.0, 120.0 };
        var calc = new MetricsCalculator();

        var (ret, risk) = calc.Calculate(closes);

        Assert.Equal(0.2, ret.CumulativeReturn, 9);
        Assert.Equal(-5.0 / 110.0, risk.MaxDrawdown, 9);
        Assert.Equal(risk.MaxDrawdown, risk.VaR95, 9);
        Assert.Equal(risk.MaxDrawdown, risk.CVaR95, 9);
        Assert.True(risk.Volatility > 0);
        Assert.Equal(risk.Volatility, ret.AnnualizedVolatility, 9);
        Assert.True(ret.Cagr > ret.CumulativeReturn);
    }

    [Fact]
    public void ConstantPrices()
    {
        var closes = new[] { 100.0, 100.0, 100.0, 100.0 };
        var calc = new MetricsCalculator();

        var (ret, risk) = calc.Calculate(closes);

        Assert.Equal(0.0, ret.CumulativeReturn, 9);
        Assert.Equal(0.0, ret.Cagr, 9);
        Assert.Equal(0.0, ret.AnnualizedVolatility, 9);
        Assert.Equal(0.0, risk.MaxDrawdown, 9);
        Assert.True(double.IsNaN(risk.SharpeRatio));
        Assert.True(risk.SortinoRatio < 0);
        Assert.True(double.IsNaN(risk.CalmarRatio));
        Assert.Equal(0.0, risk.VaR95, 9);
        Assert.Equal(0.0, risk.CVaR95, 9);
    }

    [Fact]
    public void MonotonicIncrease()
    {
        var closes = new[] { 100.0, 110.0, 121.0 };
        var calc = new MetricsCalculator();

        var (ret, risk) = calc.Calculate(closes);

        Assert.Equal(0.21, ret.CumulativeReturn, 9);
        Assert.Equal(0.0, risk.MaxDrawdown, 9);
        Assert.True(double.IsNaN(risk.SharpeRatio));
        Assert.True(double.IsNaN(risk.SortinoRatio));
        Assert.True(double.IsNaN(risk.CalmarRatio));
    }

    [Fact]
    public void MonotonicDecrease()
    {
        var closes = new[] { 100.0, 90.0, 81.0 };
        var calc = new MetricsCalculator();

        var (ret, risk) = calc.Calculate(closes);

        Assert.Equal(-0.19, ret.CumulativeReturn, 9);
        Assert.Equal(-0.19, risk.MaxDrawdown, 9);
        Assert.Equal(-0.1, risk.VaR95, 9);
        Assert.Equal(-0.1, risk.CVaR95, 9);
    }

    [Fact]
    public void InsufficientData()
    {
        var calc = new MetricsCalculator();
        Assert.Throws<InsufficientDataException>(() => calc.Calculate(Array.Empty<double>()));
        Assert.Throws<InsufficientDataException>(() => calc.Calculate(new[] { 100.0 }));
    }

    [Theory]
    [InlineData(new[] { 100.0, 0.0, 101.0 })]
    [InlineData(new[] { 100.0, -10.0, 101.0 })]
    [InlineData(new[] { 100.0, double.NaN, 101.0 })]
    [InlineData(new[] { 100.0, double.PositiveInfinity, 101.0 })]
    [InlineData(new[] { 100.0, double.NegativeInfinity, 101.0 })]
    public void InvalidPrices(double[] closes)
    {
        var calc = new MetricsCalculator();
        Assert.Throws<InvalidPricesException>(() => calc.Calculate(closes));
    }

    [Fact]
    public void RiskFreeRateAffectsSharpe()
    {
        var closes = new[] { 100.0, 101.0, 102.0, 103.0, 104.0 };
        var calcWithRf = new MetricsCalculator(0.02);
        var calcNoRf = new MetricsCalculator(0.0);

        var (_, riskWithRf) = calcWithRf.Calculate(closes);
        var (_, riskNoRf) = calcNoRf.Calculate(closes);

        Assert.False(double.IsNaN(riskWithRf.SharpeRatio));
        Assert.False(double.IsNaN(riskNoRf.SharpeRatio));
        Assert.True(riskNoRf.SharpeRatio > riskWithRf.SharpeRatio);
    }

    [Fact]
    public void JsonSerialization_ReplacesNaNWithNull()
    {
        var closes = new[] { 100.0, 100.0, 100.0 };
        var (_, risk) = new MetricsCalculator().Calculate(closes);

        var json = JsonSerializer.Serialize(risk);
        Assert.Contains("\"sharpe_ratio\":null", json);
        Assert.Contains("\"max_drawdown\":", json);
        Assert.DoesNotContain("\"max_drawdown\":null", json);
    }
}
