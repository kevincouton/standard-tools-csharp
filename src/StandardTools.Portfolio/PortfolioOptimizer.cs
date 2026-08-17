using StandardTools.Core;

namespace StandardTools.Portfolio;

public static class PortfolioOptimizer
{
    private const double CovRidge = 1e-8;
    private const double DegeneracyEps = 1e-12;

    public static PortfolioResult MeanVariance(MeanVarianceRequest request)
    {
        ValidateMeanVarianceRequest(request);

        var n = request.Returns.Count;
        var obs = request.Returns[0].Count;

        var means = request.Returns.Select(series => series.Average()).ToArray();
        var centered = request.Returns.Select(series => series.Select(r => r - series.Average()).ToArray()).ToArray();
        var cov = SampleCovariance(centered);
        for (var i = 0; i < n; i++)
            cov[i][i] += CovRidge;

        var invCov = LinearAlgebra.MatInverse(cov);
        var ones = Enumerable.Repeat(1.0, n).ToArray();

        var inv1 = LinearAlgebra.MatVecMul(invCov, ones);
        var denomMV = inv1.Sum();
        if (Math.Abs(denomMV) < DegeneracyEps)
            throw new InvalidCommandException("minimum-variance portfolio is degenerate");
        var wMV = inv1.Select(v => v / denomMV).ToArray();

        var excess = means.Select(m => m - request.RiskFreeRate).ToArray();
        var k = LinearAlgebra.MatVecMul(invCov, excess);
        var sumK = k.Sum();
        var wMS = Math.Abs(sumK) < DegeneracyEps ? wMV : k.Select(v => v / sumK).ToArray();

        var (retMV, volMV, _) = PortfolioMetrics(wMV, means, cov, request.RiskFreeRate);
        var (retMS, volMS, _) = PortfolioMetrics(wMS, means, cov, request.RiskFreeRate);

        double[] weights;
        switch (request.Objective)
        {
            case PortfolioObjective.MaxSharpe:
                weights = wMS;
                break;
            case PortfolioObjective.MinVolatility:
                weights = wMV;
                break;
            case PortfolioObjective.TargetReturn:
                if (!request.TargetReturn.HasValue)
                    throw new InvalidCommandException("target_return objective requires TargetReturn");
                var alpha = Math.Abs(retMS - retMV) >= DegeneracyEps
                    ? (request.TargetReturn.Value - retMV) / (retMS - retMV)
                    : 0.0;
                alpha = Clamp(alpha, 0.0, 1.0);
                weights = Blend(wMS, wMV, alpha);
                break;
            case PortfolioObjective.TargetVolatility:
                if (!request.TargetVolatility.HasValue)
                    throw new InvalidCommandException("target_volatility objective requires TargetVolatility");
                weights = TargetVolatilityBlend(wMS, wMV, means, cov, request.RiskFreeRate, request.TargetVolatility.Value, retMV, volMV, retMS, volMS);
                break;
            default:
                throw new InvalidCommandException($"unknown objective {request.Objective}");
        }

        var (expectedReturn, volatility, sharpe) = PortfolioMetrics(weights, means, cov, request.RiskFreeRate);
        var weightsMap = request.Labels.Select((label, i) => (label, weights[i])).ToDictionary(t => t.label, t => t.Item2);

        var result = new PortfolioResult
        {
            Weights = weightsMap,
            ExpectedReturn = expectedReturn,
            Volatility = volatility,
            SharpeRatio = sharpe
        };
        result.Validate();
        return result;
    }

    public static PortfolioResult RiskParity(RiskParityRequest request)
    {
        Validation.ValidateReturnMatrix(request.Returns, request.Labels);

        var n = request.Returns.Count;
        var obs = request.Returns[0].Count;

        var invVols = request.Returns.Select(series =>
        {
            var m = series.Average();
            var variance = series.Sum(r =>
            {
                var d = r - m;
                return d * d;
            }) / (obs - 1);
            var vol = Math.Sqrt(Math.Max(0, variance));
            return vol > DegeneracyEps ? 1.0 / vol : 0.0;
        }).ToArray();

        var total = invVols.Sum();
        if (total < DegeneracyEps)
            throw new DataQualityException("all assets have zero volatility; cannot compute risk-parity weights");

        var weights = invVols.Select(v => v / total).ToArray();
        var means = request.Returns.Select(series => series.Average()).ToArray();
        var centered = request.Returns.Select(series => series.Select(r => r - series.Average()).ToArray()).ToArray();
        var cov = SampleCovariance(centered);

        var (expectedReturn, volatility, sharpe) = PortfolioMetrics(weights, means, cov, 0.0);
        var weightsMap = request.Labels.Select((label, i) => (label, weights[i])).ToDictionary(t => t.label, t => t.Item2);

        var result = new PortfolioResult
        {
            Weights = weightsMap,
            ExpectedReturn = expectedReturn,
            Volatility = volatility,
            SharpeRatio = sharpe
        };
        result.Validate();
        return result;
    }

    public static (PortfolioResult Result, Dictionary<string, double> PosteriorReturns, double[][] PosteriorCovariance) BlackLitterman(BlackLittermanRequest request)
    {
        ValidateBlackLittermanRequest(request);

        var n = request.Returns.Count;
        var obs = request.Returns[0].Count;
        var k = request.PMatrix.Count;

        var means = request.Returns.Select(series => series.Average()).ToArray();
        var centered = request.Returns.Select(series => series.Select(r => r - series.Average()).ToArray()).ToArray();
        var cov = SampleCovariance(centered);
        for (var i = 0; i < n; i++)
            cov[i][i] += CovRidge;

        var capSum = request.MarketCaps.Sum();
        var wMkt = request.MarketCaps.Select(c => c / capSum).ToArray();

        var sigmaWMkt = LinearAlgebra.MatVecMul(cov, wMkt);
        var pi = sigmaWMkt.Select(v => v * request.RiskAversion).ToArray();

        var tauSigma = LinearAlgebra.MatScale(cov, request.Tau);
        var omegaInv = new double[k];
        for (var i = 0; i < k; i++)
        {
            var pSigma = LinearAlgebra.MatVecMul(tauSigma, request.PMatrix[i].ToArray());
            var omegaI = LinearAlgebra.VecDot(request.PMatrix[i].ToArray(), pSigma);
            if (Math.Abs(omegaI) < DegeneracyEps)
                throw new DataQualityException($"view {i} has zero confidence");
            omegaInv[i] = 1.0 / omegaI;
        }

        var ptOp = LinearAlgebra.MatAlloc(n);
        var ptOq = new double[n];
        for (var i = 0; i < k; i++)
        {
            var scale = omegaInv[i];
            var q = request.QVector[i];
            for (var ai = 0; ai < n; ai++)
            {
                for (var b = 0; b < n; b++)
                    ptOp[ai][b] += scale * request.PMatrix[i][ai] * request.PMatrix[i][b];
                ptOq[ai] += scale * q * request.PMatrix[i][ai];
            }
        }

        var tauSigmaInv = LinearAlgebra.MatInverse(tauSigma);
        var m1 = LinearAlgebra.MatAdd(tauSigmaInv, ptOp);
        var m1Inv = LinearAlgebra.MatInverse(m1);

        var m2Part = LinearAlgebra.MatVecMul(tauSigmaInv, pi);
        var m2 = m2Part.Select((v, i) => v + ptOq[i]).ToArray();

        var muBL = LinearAlgebra.MatVecMul(m1Inv, m2);
        var sigmaBL = LinearAlgebra.MatAdd(cov, m1Inv);

        var a = LinearAlgebra.MatScale(sigmaBL, request.RiskAversion);
        var aInv = LinearAlgebra.MatInverse(a);
        var wRaw = LinearAlgebra.MatVecMul(aInv, muBL);
        var wSum = wRaw.Sum();
        if (Math.Abs(wSum) < DegeneracyEps)
            throw new InvalidCommandException("optimised weights sum to zero");
        var w = wRaw.Select(v => v / wSum).ToArray();

        var weightsMap = request.Labels.Select((label, i) => (label, w[i])).ToDictionary(t => t.label, t => t.Item2);
        var expectedMap = request.Labels.Select((label, i) => (label, muBL[i])).ToDictionary(t => t.label, t => t.Item2);

        var (expectedReturn, volatility, sharpe) = PortfolioMetrics(w, muBL, sigmaBL, 0.0);
        var result = new PortfolioResult
        {
            Weights = weightsMap,
            ExpectedReturn = expectedReturn,
            Volatility = volatility,
            SharpeRatio = sharpe
        };
        result.Validate();
        return (result, expectedMap, sigmaBL);
    }

    public static (PortfolioResult Result, Dictionary<string, double> PosteriorReturns, double[][] PosteriorCovariance) BlackLittermanSimplified(BlackLittermanSimplifiedRequest request)
    {
        Validation.ValidateReturnMatrix(request.Returns, request.Labels);
        if (request.Labels.Count == 0)
            throw new DataQualityException("labels must not be empty");
        if (request.Views.Count == 0)
            throw new InvalidCommandException("at least one expert view is required");
        if (request.Tau <= 0 || !Validation.IsFinite(request.Tau))
            throw new InvalidCommandException("tau must be a positive finite number");
        if (request.RiskAversion <= 0 || !Validation.IsFinite(request.RiskAversion))
            throw new InvalidCommandException("risk_aversion must be a positive finite number");

        var orderedCaps = new double[request.Labels.Count];
        for (var i = 0; i < request.Labels.Count; i++)
        {
            if (!request.MarketCaps.TryGetValue(request.Labels[i], out var cap))
                throw new InvalidCommandException($"missing market cap for asset {request.Labels[i]}");
            if (cap <= 0 || !Validation.IsFinite(cap))
                throw new InvalidCommandException($"market cap for {request.Labels[i]} must be positive and finite");
            orderedCaps[i] = cap;
        }

        var pRows = new List<double[]>();
        var q = new List<double>();
        foreach (var (label, expectedReturn) in request.Views)
        {
            var idx = request.Labels.Select((l, i) => (l, i)).FirstOrDefault(t => t.l == label).i;
            if (idx < 0 || idx >= request.Labels.Count || request.Labels[idx] != label)
                throw new InvalidCommandException($"unknown view asset {label}");
            var row = new double[request.Labels.Count];
            row[idx] = 1.0;
            pRows.Add(row);
            q.Add(expectedReturn);
        }

        return BlackLitterman(new BlackLittermanRequest
        {
            Returns = request.Returns,
            Labels = request.Labels,
            MarketCaps = orderedCaps,
            PMatrix = pRows,
            QVector = q,
            Tau = request.Tau,
            RiskAversion = request.RiskAversion
        });
    }

    private static void ValidateMeanVarianceRequest(MeanVarianceRequest request)
    {
        if (!Validation.IsFinite(request.RiskFreeRate))
            throw new InvalidCommandException("risk_free_rate must be finite");
        Validation.ValidateReturnMatrix(request.Returns, request.Labels);

        switch (request.Objective)
        {
            case PortfolioObjective.MaxSharpe:
            case PortfolioObjective.MinVolatility:
                break;
            case PortfolioObjective.TargetReturn:
                if (!request.TargetReturn.HasValue || !Validation.IsFinite(request.TargetReturn.Value))
                    throw new InvalidCommandException("target_return must be a finite number");
                break;
            case PortfolioObjective.TargetVolatility:
                if (!request.TargetVolatility.HasValue || !Validation.IsFinite(request.TargetVolatility.Value) || request.TargetVolatility.Value < 0)
                    throw new InvalidCommandException("target_volatility must be a finite non-negative number");
                break;
            default:
                throw new InvalidCommandException($"unknown objective {request.Objective}");
        }
    }

    private static void ValidateBlackLittermanRequest(BlackLittermanRequest request)
    {
        if (request.Tau <= 0 || !Validation.IsFinite(request.Tau))
            throw new InvalidCommandException("tau must be a positive finite number");
        if (request.RiskAversion <= 0 || !Validation.IsFinite(request.RiskAversion))
            throw new InvalidCommandException("risk_aversion must be a positive finite number");
        Validation.ValidateReturnMatrix(request.Returns, request.Labels);

        var n = request.Returns.Count;
        if (request.MarketCaps.Count != n)
            throw new InvalidCommandException($"expected {n} market caps, got {request.MarketCaps.Count}");
        for (var i = 0; i < request.MarketCaps.Count; i++)
        {
            if (request.MarketCaps[i] <= 0 || !Validation.IsFinite(request.MarketCaps[i]))
                throw new InvalidCommandException($"market cap {i} must be positive and finite");
        }

        if (request.PMatrix.Count == 0)
            throw new InvalidCommandException("P matrix must contain at least one view");
        if (request.PMatrix.Count != request.QVector.Count)
            throw new InvalidCommandException($"P matrix has {request.PMatrix.Count} rows but Q has {request.QVector.Count} elements");
        for (var i = 0; i < request.PMatrix.Count; i++)
        {
            if (request.PMatrix[i].Count != n)
                throw new InvalidCommandException($"P row {i} has length {request.PMatrix[i].Count} but there are {n} assets");
            if (request.PMatrix[i].All(v => v == 0))
                throw new InvalidCommandException($"P row {i} is all zeros");
        }
    }

    private static (double ExpectedReturn, double Volatility, double Sharpe) PortfolioMetrics(double[] w, double[] means, double[][] cov, double rf)
    {
        var expectedReturn = LinearAlgebra.VecDot(w, means);
        var variance = 0.0;
        for (var i = 0; i < w.Length; i++)
            for (var j = 0; j < w.Length; j++)
                variance += w[i] * cov[i][j] * w[j];
        var volatility = Math.Sqrt(Math.Max(0, variance));
        var sharpe = volatility > 0 ? (expectedReturn - rf) / volatility : double.NegativeInfinity;
        return (expectedReturn, volatility, sharpe);
    }

    private static double[] TargetVolatilityBlend(double[] wMS, double[] wMV, double[] means, double[][] cov, double rf, double targetVol, double retMV, double volMV, double retMS, double volMS)
    {
        var target = Math.Max(volMV, Math.Min(volMS, targetVol));
        if (target <= volMV || Math.Abs(volMS - volMV) < DegeneracyEps)
            return wMV;
        if (target >= volMS)
            return wMS;

        var varMS = volMS * volMS;
        var varMV = volMV * volMV;
        var sigmaWMV = LinearAlgebra.MatVecMul(cov, wMV);
        var covMSMV = LinearAlgebra.VecDot(wMS, sigmaWMV);
        var targetVar = target * target;
        var a = varMS + varMV - 2 * covMSMV;
        var b = 2 * covMSMV - 2 * varMV;
        var c = varMV - targetVar;

        var alpha = SolveQuadraticForBlend(a, b, c);
        return Blend(wMS, wMV, alpha);
    }

    private static double SolveQuadraticForBlend(double a, double b, double c)
    {
        var discriminant = b * b - 4 * a * c;
        if (discriminant < 0 || Math.Abs(a) < DegeneracyEps)
        {
            if (Math.Abs(b) < DegeneracyEps)
                return 0.5;
            return Clamp(-c / b, 0.0, 1.0);
        }
        var sqrtD = Math.Sqrt(discriminant);
        var alpha1 = (-b + sqrtD) / (2 * a);
        var alpha2 = (-b - sqrtD) / (2 * a);
        return Clamp(Math.Max(alpha1, alpha2), 0.0, 1.0);
    }

    private static double[][] SampleCovariance(IReadOnlyList<double[]> centered)
    {
        var n = centered.Count;
        var obs = centered[0].Length;
        var cov = LinearAlgebra.MatAlloc(n);
        var scale = 1.0 / (obs - 1);
        for (var i = 0; i < n; i++)
        {
            for (var j = i; j < n; j++)
            {
                var sum = 0.0;
                for (var t = 0; t < obs; t++)
                    sum += centered[i][t] * centered[j][t];
                cov[i][j] = sum * scale;
                cov[j][i] = cov[i][j];
            }
        }
        return cov;
    }

    private static double[] Blend(double[] a, double[] b, double alpha) =>
        a.Select((v, i) => alpha * v + (1 - alpha) * b[i]).ToArray();

    private static double Clamp(double v, double min, double max) =>
        v < min ? min : v > max ? max : v;
}
