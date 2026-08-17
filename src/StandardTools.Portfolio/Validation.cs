using StandardTools.Core;

namespace StandardTools.Portfolio;

internal static class Validation
{
    public static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

    public static void ValidateReturnMatrix(IReadOnlyList<IReadOnlyList<double>> returns, IReadOnlyList<string> labels)
    {
        if (returns.Count == 0)
            throw new DataQualityException("returns matrix must contain at least one series");
        if (labels.Count != returns.Count)
            throw new InvalidCommandException($"expected {returns.Count} labels, got {labels.Count}");

        var seen = new HashSet<string>();
        foreach (var label in labels)
        {
            if (!seen.Add(label))
                throw new InvalidCommandException($"duplicate label {label}");
        }

        var obs = returns[0].Count;
        if (obs < 2)
            throw new DataQualityException("each return series must contain at least two observations");

        for (var i = 0; i < returns.Count; i++)
        {
            if (returns[i].Count != obs)
                throw new DataQualityException($"series {i} has length {returns[i].Count} but series 0 has length {obs}");
            for (var j = 0; j < obs; j++)
            {
                if (!IsFinite(returns[i][j]))
                    throw new DataQualityException($"series {i} contains non-finite value at index {j}");
            }
        }
    }
}
