using StandardTools.Core;

namespace StandardTools.Portfolio;

internal static class LinearAlgebra
{
    public static double[][] MatAlloc(int n)
    {
        var m = new double[n][];
        for (var i = 0; i < n; i++)
            m[i] = new double[n];
        return m;
    }

    public static double[][] MatCopy(double[][] m)
    {
        var n = m.Length;
        var copy = new double[n][];
        for (var i = 0; i < n; i++)
        {
            copy[i] = new double[m[i].Length];
            Array.Copy(m[i], copy[i], m[i].Length);
        }
        return copy;
    }

    public static double[] MatVecMul(double[][] a, double[] x)
    {
        var m = a.Length;
        var result = new double[m];
        for (var i = 0; i < m; i++)
        {
            var sum = 0.0;
            for (var j = 0; j < x.Length; j++)
                sum += a[i][j] * x[j];
            result[i] = sum;
        }
        return result;
    }

    public static double VecDot(double[] a, double[] b)
    {
        var sum = 0.0;
        for (var i = 0; i < a.Length; i++)
            sum += a[i] * b[i];
        return sum;
    }

    public static double[][] MatInverse(double[][] m)
    {
        var n = m.Length;
        if (n == 0)
            throw new InvalidCommandException("cannot invert empty matrix");
        if (m.Any(row => row.Length != n))
            throw new InvalidCommandException("matrix must be square");

        var aug = MatCopy(m);
        for (var i = 0; i < n; i++)
        {
            Array.Resize(ref aug[i], 2 * n);
            aug[i][n + i] = 1.0;
        }

        for (var col = 0; col < n; col++)
        {
            var pivot = col;
            var maxVal = Math.Abs(aug[col][col]);
            for (var row = col + 1; row < n; row++)
            {
                if (Math.Abs(aug[row][col]) > maxVal)
                {
                    maxVal = Math.Abs(aug[row][col]);
                    pivot = row;
                }
            }

            if (maxVal < 1e-15)
                throw new InvalidCommandException("matrix is singular or near-singular");

            if (pivot != col)
                (aug[col], aug[pivot]) = (aug[pivot], aug[col]);

            var pivotVal = aug[col][col];
            for (var j = 0; j < 2 * n; j++)
                aug[col][j] /= pivotVal;

            for (var row = 0; row < n; row++)
            {
                if (row == col) continue;
                var factor = aug[row][col];
                if (factor == 0) continue;
                for (var j = col; j < 2 * n; j++)
                    aug[row][j] -= factor * aug[col][j];
            }
        }

        var inv = MatAlloc(n);
        for (var i = 0; i < n; i++)
            for (var j = 0; j < n; j++)
                inv[i][j] = aug[i][n + j];
        return inv;
    }

    public static double[][] MatScale(double[][] m, double s)
    {
        var n = m.Length;
        var result = MatAlloc(n);
        for (var i = 0; i < n; i++)
            for (var j = 0; j < n; j++)
                result[i][j] = m[i][j] * s;
        return result;
    }

    public static double[][] MatAdd(double[][] a, double[][] b)
    {
        var n = a.Length;
        var result = MatAlloc(n);
        for (var i = 0; i < n; i++)
            for (var j = 0; j < n; j++)
                result[i][j] = a[i][j] + b[i][j];
        return result;
    }
}
