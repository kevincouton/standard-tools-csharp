namespace StandardTools.Backtest;

public sealed record ConfidenceInterval(double Lower, double Upper);

public sealed record MonteCarloResult(
    int Simulations,
    ConfidenceInterval FinalEquityCI,
    ConfidenceInterval MaxDrawdownCI,
    double InitialCapital);

/// <summary>
/// Reshuffles period or trade returns to estimate the distribution of backtest outcomes.
/// </summary>
public sealed class MonteCarloSimulator
{
    public const int MaxSimulations = 100_000;

    private readonly int _simulations;
    private readonly int? _seed;

    public MonteCarloSimulator(int simulations, int? seed = null)
    {
        _simulations = Math.Clamp(simulations, 0, MaxSimulations);
        _seed = seed;
    }

    public MonteCarloResult FromTrades(IReadOnlyList<Trade> trades, decimal initialCapital)
    {
        var returns = trades.Select(t =>
        {
            var cost = t.EntryPrice * t.Quantity;
            return cost == 0 ? 0.0 : (double)(t.PnL / cost);
        }).ToArray();
        return FromReturns(returns, initialCapital);
    }

    public MonteCarloResult FromReturns(IReadOnlyList<double> returns, decimal initialCapital)
    {
        var initial = (double)initialCapital;
        if (returns.Count == 0 || _simulations == 0)
        {
            return new MonteCarloResult(
                _simulations,
                new ConfidenceInterval(initial, initial),
                new ConfidenceInterval(0, 0),
                initial);
        }

        var rng = _seed.HasValue ? new Random(_seed.Value) : new Random();
        var finalEquities = new List<double>(_simulations);
        var maxDrawdowns = new List<double>(_simulations);

        for (var i = 0; i < _simulations; i++)
        {
            var shuffled = returns.ToArray();
            Shuffle(rng, shuffled);
            var (equity, maxDD) = SimulatePath(shuffled, initial);
            finalEquities.Add(equity);
            maxDrawdowns.Add(maxDD);
        }

        finalEquities.Sort();
        maxDrawdowns.Sort();

        return new MonteCarloResult(
            _simulations,
            new ConfidenceInterval(Percentile(finalEquities, 0.05), Percentile(finalEquities, 0.95)),
            new ConfidenceInterval(Percentile(maxDrawdowns, 0.05), Percentile(maxDrawdowns, 0.95)),
            initial);
    }

    private static void Shuffle(Random rng, double[] values)
    {
        for (var i = values.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    private static (double Equity, double MaxDrawdown) SimulatePath(double[] returns, double initial)
    {
        var equity = initial;
        var peak = equity;
        var maxDD = 0.0;
        foreach (var r in returns)
        {
            equity *= 1.0 + r;
            if (equity > peak) peak = equity;
            if (peak > 0)
            {
                var dd = (peak - equity) / peak;
                if (dd > maxDD) maxDD = dd;
            }
        }
        return (equity, -maxDD);
    }

    private static double Percentile(IReadOnlyList<double> values, double quantile)
    {
        if (values.Count == 0) return 0;
        var index = (int)Math.Round(quantile * (values.Count - 1));
        index = Math.Clamp(index, 0, values.Count - 1);
        return values[index];
    }
}
