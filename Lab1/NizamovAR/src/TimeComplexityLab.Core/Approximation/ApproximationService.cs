using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core.Approximation;

public sealed class ApproximationService
{
    /// <summary>
    /// Подбор C по МНК: C = Σ(T_i · f(n_i)) / Σ(f(n_i)²)
    /// </summary>
    public ApproximationResult Fit(
        IReadOnlyList<(int N, double T)> points,
        ComplexityFunction function)
    {
        if (points.Count == 0)
            return new ApproximationResult(function, 0, 0, 0);

        double numerator = 0;
        double denominator = 0;

        foreach (var (n, t) in points)
        {
            double f = function.Evaluate(n);
            numerator += t * f;
            denominator += f * f;
        }

        double c = denominator > 0 ? numerator / denominator : 0;

        double mse = 0;
        foreach (var (n, t) in points)
        {
            double diff = t - c * function.Evaluate(n);
            mse += diff * diff;
        }
        mse /= points.Count;

        return new ApproximationResult(function, c, mse, points.Count);
    }

    /// <summary>
    /// Точки теоретической кривой для отрисовки на графике.
    /// </summary>
    public IReadOnlyList<(int N, double T)> BuildCurve(
        IReadOnlyList<int> nValues,
        ApproximationResult result)
    {
        var curve = new List<(int, double)>(nValues.Count);
        foreach (var n in nValues)
        {
            curve.Add((n, result.C * result.Function.Evaluate(n)));
        }
        return curve;
    }
}