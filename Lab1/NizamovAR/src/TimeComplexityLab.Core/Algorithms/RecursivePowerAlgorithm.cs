using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core.Algorithms;

public class RecursivePowerAlgorithm : IAlgorithm
{
    public string Id => "power.recursive";
    public string DisplayName => "Возведение в степень (рекурсивно)";
    public string Category => "Возведение в степень";
    public string TheoreticalComplexity => "O(n)";
    public ExperimentDefaults Defaults => new(
    NMax: 1_000, NStep: 10, MMax: 0, MStep: 0,
    RunsPerPoint: 1, Hypothesis: "Linear");

    public ExperimentDimension Dimension => ExperimentDimension.OneDimensional;
    public MeasureUnit MeasureUnit => MeasureUnit.Steps;

    public object GenerateInput(ExperimentPoint point, Random random)
    {
        return (X: 1.0001, N: point.N);
    }

    public void Execute(object input, IExecutionTracker tracker)
    {
        var (x, n) = ((double X, int N))input;
        double result = Power(x, n, tracker);
        GC.KeepAlive(result);
    }

    private static double Power(double x, int n, IExecutionTracker tracker)
    {
        if (n == 0) return 1.0;
        tracker.CountStep();   // один рекурсивный вызов = один шаг
        return x * Power(x, n - 1, tracker);
    }
}