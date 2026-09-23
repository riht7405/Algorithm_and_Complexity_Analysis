using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core.Algorithms;

public class IterativePowerAlgorithm : IAlgorithm
{
    public string Id => "power.iterative";
    public string DisplayName => "Возведение в степень (итеративно)";
    public string Category => "Возведение в степень";
    public string TheoreticalComplexity => "O(n)";

    public ExperimentDimension Dimension => ExperimentDimension.OneDimensional;
    public MeasureUnit MeasureUnit => MeasureUnit.Steps;

    public ExperimentDefaults Defaults => new(
        NMax: 2_000, NStep: 20, MMax: 0, MStep: 0,
        RunsPerPoint: 5, Hypothesis: "Linear");

    public object GenerateSource(ExperimentPoint maxPoint, Random random)
        => (X: 1.0001, MaxN: maxPoint.N);

    public object Slice(object source, ExperimentPoint point)
        => (X: 1.0001, N: point.N);

    public object Clone(object input) => input;

    public void Execute(object input, IExecutionTracker tracker)
    {
        var (x, n) = ((double X, int N))input;

        if (n == 0) { GC.KeepAlive(1.0); return; }

        double result = x;
        for (int i = 1; i < n; i++)
        {
            result *= x;
            tracker.CountStep();
        }
        GC.KeepAlive(result);
    }
}