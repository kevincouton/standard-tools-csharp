using System.Text.Json;
using System.Text.Json.Serialization;

namespace StandardTools.Analysis;

public sealed record LinearRegressionResult(double Slope, double Intercept, double RSquared);
public sealed record CorrelationResult(double Pearson);
public sealed record CointegrationResult(bool IsCointegrated, double HedgeRatio, double Intercept, double ResidualHurst);
public sealed record HurstResult(double HurstExponent);
public sealed record PcaResult(IReadOnlyList<double> Eigenvalues, IReadOnlyList<IReadOnlyList<double>> Eigenvectors, IReadOnlyList<double> ExplainedVarianceRatios);
public sealed record MultiFactorResult(double Alpha, IReadOnlyList<double> Betas, double RSquared);

[JsonConverter(typeof(OptionsResultJsonConverter))]
public sealed record OptionsResult(double Price, double Delta, double Gamma, double Theta, double Vega, double Rho);

internal sealed class OptionsResultJsonConverter : JsonConverter<OptionsResult>
{
    public override OptionsResult Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException();

    public override void Write(Utf8JsonWriter writer, OptionsResult value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("price"); WriteDouble(writer, value.Price);
        writer.WritePropertyName("delta"); WriteDouble(writer, value.Delta);
        writer.WritePropertyName("gamma"); WriteDouble(writer, value.Gamma);
        writer.WritePropertyName("theta"); WriteDouble(writer, value.Theta);
        writer.WritePropertyName("vega"); WriteDouble(writer, value.Vega);
        writer.WritePropertyName("rho"); WriteDouble(writer, value.Rho);
        writer.WriteEndObject();
    }

    private static void WriteDouble(Utf8JsonWriter writer, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) writer.WriteNullValue();
        else writer.WriteNumberValue(value);
    }
}

/// <summary>
/// Unified entry point for quantitative analysis calculations.
/// </summary>
public sealed class AnalysisCalculator
{
    public LinearRegressionResult LinearRegression(IReadOnlyList<double> xs, IReadOnlyList<double> ys)
    {
        ValidateEqualLength(xs, ys, 2);
        var n = xs.Count;
        var meanX = Mean(xs);
        var meanY = Mean(ys);

        var sxy = 0.0;
        var sxx = 0.0;
        var syy = 0.0;
        for (var i = 0; i < n; i++)
        {
            var dx = xs[i] - meanX;
            var dy = ys[i] - meanY;
            sxy += dx * dy;
            sxx += dx * dx;
            syy += dy * dy;
        }

        if (sxx == 0)
            throw new InsufficientDataException("x values must have non-zero variance");

        var slope = sxy / sxx;
        var intercept = meanY - slope * meanX;
        var rSquared = syy == 0 ? 1.0 : (sxy * sxy) / (sxx * syy);
        return new LinearRegressionResult(slope, intercept, rSquared);
    }

    public CorrelationResult Correlation(IReadOnlyList<double> xs, IReadOnlyList<double> ys)
    {
        ValidateEqualLength(xs, ys, 2);
        var regression = LinearRegression(xs, ys);
        var sign = regression.Slope >= 0 ? 1.0 : -1.0;
        return new CorrelationResult(sign * Math.Sqrt(Math.Clamp(regression.RSquared, 0.0, 1.0)));
    }

    public CointegrationResult Cointegration(IReadOnlyList<double> x, IReadOnlyList<double> y, double hurstThreshold = 0.45)
    {
        ValidateEqualLength(x, y, 2);
        var regression = LinearRegression(x, y);
        var residuals = new double[x.Count];
        for (var i = 0; i < x.Count; i++)
            residuals[i] = y[i] - (regression.Slope * x[i] + regression.Intercept);

        var hurst = CalculateHurst(residuals, useLogReturns: false);
        var isCointegrated = hurst.HurstExponent < hurstThreshold && regression.RSquared > 0.5;
        return new CointegrationResult(isCointegrated, regression.Slope, regression.Intercept, hurst.HurstExponent);
    }

    public HurstResult Hurst(IReadOnlyList<double> values)
    {
        if (values.Count < 8)
            throw new InsufficientDataException("at least 8 observations required for Hurst exponent");
        return CalculateHurst(values, useLogReturns: true);
    }

    private static HurstResult CalculateHurst(IReadOnlyList<double> values, bool useLogReturns)
    {
        double[] series;
        if (useLogReturns)
        {
            series = new double[values.Count - 1];
            for (var i = 1; i < values.Count; i++)
                series[i - 1] = Math.Log(values[i] / values[i - 1]);
        }
        else
        {
            series = values.ToArray();
        }

        var maxLag = Math.Min(series.Length / 2, 100);
        var lags = Enumerable.Range(2, maxLag - 1).Select(i => (double)i).ToArray();
        var rsValues = new double[lags.Length];

        for (var i = 0; i < lags.Length; i++)
        {
            var lag = (int)lags[i];
            rsValues[i] = RescaledRange(series, lag);
        }

        var logLags = lags.Select(v => Math.Log(v)).ToArray();
        var logRs = rsValues.Select(v => Math.Log(v)).ToArray();
        var regression = SimpleLinearRegression(logLags, logRs);
        return new HurstResult(regression.Slope);
    }

    private static double RescaledRange(IReadOnlyList<double> values, int lag)
    {
        var n = values.Count;
        var chunks = n / lag;
        var rsSum = 0.0;
        var count = 0;

        for (var c = 0; c < chunks; c++)
        {
            var chunk = new double[lag];
            for (var i = 0; i < lag; i++)
                chunk[i] = values[c * lag + i];

            var mean = chunk.Average();
            var cumulative = 0.0;
            var maxDeviation = double.MinValue;
            var minDeviation = double.MaxValue;
            var sumSq = 0.0;

            foreach (var v in chunk)
            {
                cumulative += v - mean;
                maxDeviation = Math.Max(maxDeviation, cumulative);
                minDeviation = Math.Min(minDeviation, cumulative);
                var d = v - mean;
                sumSq += d * d;
            }

            var s = Math.Sqrt(sumSq / lag);
            if (s == 0) continue;

            rsSum += (maxDeviation - minDeviation) / s;
            count++;
        }

        return count == 0 ? 0 : rsSum / count;
    }

    public PcaResult Pca(IReadOnlyList<IReadOnlyList<double>> series, int components = 2)
    {
        if (series.Count == 0 || series[0].Count == 0)
            throw new InsufficientDataException("non-empty series required");
        var n = series[0].Count;
        if (series.Any(s => s.Count != n))
            throw new InsufficientDataException("all series must have the same length");
        if (components > series.Count)
            components = series.Count;

        var mean = series.Select(s => s.Average()).ToArray();
        var cov = CovarianceMatrix(series, mean);

        var eigen = PowerIterationEigenvalues(cov, components);
        var total = eigen.Eigenvalues.Sum();
        var explained = eigen.Eigenvalues.Select(ev => total == 0 ? 0 : ev / total).ToArray();

        return new PcaResult(eigen.Eigenvalues, eigen.Eigenvectors, explained);
    }

    private static double[,] CovarianceMatrix(IReadOnlyList<IReadOnlyList<double>> series, double[] mean)
    {
        var d = series.Count;
        var n = series[0].Count;
        var cov = new double[d, d];
        for (var i = 0; i < d; i++)
        {
            for (var j = 0; j < d; j++)
            {
                var sum = 0.0;
                for (var k = 0; k < n; k++)
                    sum += (series[i][k] - mean[i]) * (series[j][k] - mean[j]);
                cov[i, j] = sum / (n - 1);
            }
        }
        return cov;
    }

    private static (double[] Eigenvalues, IReadOnlyList<IReadOnlyList<double>> Eigenvectors) PowerIterationEigenvalues(double[,] matrix, int components)
    {
        var d = matrix.GetLength(0);
        var eigenvalues = new double[components];
        var eigenvectors = new List<IReadOnlyList<double>>(components);
        var working = (double[,])matrix.Clone();

        for (var c = 0; c < components; c++)
        {
            var vector = Enumerable.Range(0, d).Select(_ => Random.Shared.NextDouble() - 0.5).ToArray();
            Normalize(vector);

            for (var iter = 0; iter < 100; iter++)
            {
                var next = MatrixVectorProduct(working, vector);
                Normalize(next);
                vector = next;
            }

            var eigenvalue = RayleighQuotient(working, vector);
            eigenvalues[c] = eigenvalue;
            eigenvectors.Add(vector.ToArray());

            // Deflate matrix for next component.
            for (var i = 0; i < d; i++)
                for (var j = 0; j < d; j++)
                    working[i, j] -= eigenvalue * vector[i] * vector[j];
        }

        return (eigenvalues, eigenvectors);
    }

    private static double[] MatrixVectorProduct(double[,] matrix, double[] vector)
    {
        var d = matrix.GetLength(0);
        var result = new double[d];
        for (var i = 0; i < d; i++)
            for (var j = 0; j < d; j++)
                result[i] += matrix[i, j] * vector[j];
        return result;
    }

    private static double RayleighQuotient(double[,] matrix, double[] vector)
    {
        var av = MatrixVectorProduct(matrix, vector);
        var num = 0.0;
        var denom = 0.0;
        for (var i = 0; i < vector.Length; i++)
        {
            num += vector[i] * av[i];
            denom += vector[i] * vector[i];
        }
        return denom == 0 ? 0 : num / denom;
    }

    private static void Normalize(double[] vector)
    {
        var norm = Math.Sqrt(vector.Sum(v => v * v));
        if (norm == 0) return;
        for (var i = 0; i < vector.Length; i++)
            vector[i] /= norm;
    }

    public MultiFactorResult MultiFactor(IReadOnlyList<double> assetReturns, IReadOnlyList<IReadOnlyList<double>> factorReturns)
    {
        if (factorReturns.Count == 0)
            throw new InsufficientDataException("at least one factor is required");
        var n = assetReturns.Count;
        if (factorReturns.Any(f => f.Count != n))
            throw new InsufficientDataException("all factor return series must match asset return length");

        // Add intercept as first column.
        var columns = new List<IReadOnlyList<double>> { Enumerable.Repeat(1.0, n).ToArray() };
        columns.AddRange(factorReturns);

        var xtx = new double[columns.Count, columns.Count];
        var xty = new double[columns.Count];
        for (var i = 0; i < columns.Count; i++)
        {
            for (var j = 0; j < columns.Count; j++)
            {
                var sum = 0.0;
                for (var k = 0; k < n; k++)
                    sum += columns[i][k] * columns[j][k];
                xtx[i, j] = sum;
            }
            var sumY = 0.0;
            for (var k = 0; k < n; k++)
                sumY += columns[i][k] * assetReturns[k];
            xty[i] = sumY;
        }

        var coefficients = SolveLinearSystem(xtx, xty);
        var alpha = coefficients[0];
        var betas = coefficients.Skip(1).ToArray();

        var predictions = new double[n];
        var ssTot = 0.0;
        var ssRes = 0.0;
        var meanY = assetReturns.Average();
        for (var k = 0; k < n; k++)
        {
            predictions[k] = coefficients[0];
            for (var i = 1; i < coefficients.Length; i++)
                predictions[k] += coefficients[i] * factorReturns[i - 1][k];
            ssTot += (assetReturns[k] - meanY) * (assetReturns[k] - meanY);
            ssRes += (assetReturns[k] - predictions[k]) * (assetReturns[k] - predictions[k]);
        }

        var rSquared = ssTot == 0 ? 1.0 : 1.0 - ssRes / ssTot;
        return new MultiFactorResult(alpha, betas, rSquared);
    }

    private static double[] SolveLinearSystem(double[,] a, double[] b)
    {
        var n = b.Length;
        var m = new double[n, n + 1];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
                m[i, j] = a[i, j];
            m[i, n] = b[i];
        }

        for (var col = 0; col < n; col++)
        {
            var pivot = col;
            for (var row = col + 1; row < n; row++)
                if (Math.Abs(m[row, col]) > Math.Abs(m[pivot, col]))
                    pivot = row;

            if (Math.Abs(m[pivot, col]) < 1e-12)
                throw new InsufficientDataException("factor matrix is singular");

            for (var j = 0; j <= n; j++)
                (m[col, j], m[pivot, j]) = (m[pivot, j], m[col, j]);

            for (var row = 0; row < n; row++)
            {
                if (row == col) continue;
                var factor = m[row, col] / m[col, col];
                for (var j = col; j <= n; j++)
                    m[row, j] -= factor * m[col, j];
            }
        }

        var result = new double[n];
        for (var i = 0; i < n; i++)
            result[i] = m[i, n] / m[i, i];
        return result;
    }

    public OptionsResult Options(string optionType, double spot, double strike, double riskFreeRate, double volatility, double timeToExpiry)
    {
        if (timeToExpiry <= 0 || volatility <= 0 || spot <= 0 || strike <= 0)
            throw new InsufficientDataException("spot, strike, volatility, and time to expiry must be positive");

        var isCall = optionType.Equals("call", StringComparison.OrdinalIgnoreCase);
        var d1 = (Math.Log(spot / strike) + (riskFreeRate + 0.5 * volatility * volatility) * timeToExpiry) / (volatility * Math.Sqrt(timeToExpiry));
        var d2 = d1 - volatility * Math.Sqrt(timeToExpiry);

        var nd1 = NormalCdf(d1);
        var nd2 = NormalCdf(d2);
        var nPd1 = NormalPdf(d1);

        var price = isCall
            ? spot * nd1 - strike * Math.Exp(-riskFreeRate * timeToExpiry) * nd2
            : strike * Math.Exp(-riskFreeRate * timeToExpiry) * NormalCdf(-d2) - spot * NormalCdf(-d1);

        var delta = isCall ? nd1 : nd1 - 1;
        var gamma = nPd1 / (spot * volatility * Math.Sqrt(timeToExpiry));
        var theta = -(spot * nPd1 * volatility) / (2 * Math.Sqrt(timeToExpiry))
            - riskFreeRate * strike * Math.Exp(-riskFreeRate * timeToExpiry) * (isCall ? nd2 : NormalCdf(-d2));
        theta /= 365.0;
        var vega = spot * nPd1 * Math.Sqrt(timeToExpiry) / 100.0;
        var rho = isCall
            ? strike * timeToExpiry * Math.Exp(-riskFreeRate * timeToExpiry) * nd2 / 100.0
            : -strike * timeToExpiry * Math.Exp(-riskFreeRate * timeToExpiry) * NormalCdf(-d2) / 100.0;

        return new OptionsResult(price, delta, gamma, theta, vega, rho);
    }

    private static double NormalCdf(double x)
    {
        // Abramowitz & Stegun approximation.
        var a1 = 0.254829592;
        var a2 = -0.284496736;
        var a3 = 1.421413741;
        var a4 = -1.453152027;
        var a5 = 1.061405429;
        var p = 0.3275911;

        var sign = x < 0 ? -1 : 1;
        x = Math.Abs(x) / Math.Sqrt(2.0);

        var t = 1.0 / (1.0 + p * x);
        var y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

        return 0.5 * (1.0 + sign * y);
    }

    private static double NormalPdf(double x) => Math.Exp(-0.5 * x * x) / Math.Sqrt(2.0 * Math.PI);

    private static double Mean(IReadOnlyList<double> values) => values.Sum() / values.Count;

    private static LinearRegressionResult SimpleLinearRegression(IReadOnlyList<double> xs, IReadOnlyList<double> ys)
    {
        var n = xs.Count;
        var meanX = xs.Average();
        var meanY = ys.Average();
        var sxy = 0.0;
        var sxx = 0.0;
        for (var i = 0; i < n; i++)
        {
            var dx = xs[i] - meanX;
            var dy = ys[i] - meanY;
            sxy += dx * dy;
            sxx += dx * dx;
        }
        var slope = sxx == 0 ? 0 : sxy / sxx;
        var intercept = meanY - slope * meanX;
        var syy = 0.0;
        for (var i = 0; i < n; i++)
        {
            var dy = ys[i] - meanY;
            syy += dy * dy;
        }
        var rSquared = syy == 0 ? 1.0 : (sxy * sxy) / (sxx * syy);
        return new LinearRegressionResult(slope, intercept, rSquared);
    }

    private static void ValidateEqualLength(IReadOnlyList<double> a, IReadOnlyList<double> b, int minLength)
    {
        if (a.Count != b.Count)
            throw new InsufficientDataException("input series must have equal length");
        if (a.Count < minLength)
            throw new InsufficientDataException($"at least {minLength} observations required");
    }
}
