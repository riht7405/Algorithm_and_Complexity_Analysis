using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core.Algorithms;

public class RecursivePowerAlgorithm : IAlgorithm
{
    public string Id => "power.recursive";
    public string DisplayName => "Возведение в степень (рекурсивно)";
    public string Category => "Возведение в степень";
    public string TheoreticalComplexity => "O(n)";

    public ExperimentDimension Dimension => ExperimentDimension.OneDimensional;
    public MeasureUnit MeasureUnit => MeasureUnit.Steps;

    public ExperimentDefaults Defaults => new(
        NMax: 1_000, NStep: 10, MMax: 0, MStep: 0,
        RunsPerPoint: 5, Hypothesis: "Linear");

    public object GenerateSource(ExperimentPoint maxPoint, Random random)
        => (X: 1.0001, MaxN: maxPoint.N);

    public object Slice(object source, ExperimentPoint point)
        => (X: 1.0001, N: point.N);

    public object Clone(object input) => input;

    public void Execute(object input, IExecutionTracker tracker)
    {
        var (x, n) = ((double X, int N))input;
        double result = Power(x, n, tracker);
        GC.KeepAlive(result);
    }

    /// <summary>
    /// x^n = x · x^(n-1), база x^0 = 1, x^1 = x.
    /// Считаем УМНОЖЕНИЯ (не рекурсивные вызовы),
    /// чтобы метрика совпадала с итеративной версией.
    /// </summary>
    private static double Power(double x, int n, IExecutionTracker tracker)
    {
        if (n == 0) return 1.0;
        if (n == 1) return x;

        tracker.CountStep();
        return x * Power(x, n - 1, tracker);
    }
}