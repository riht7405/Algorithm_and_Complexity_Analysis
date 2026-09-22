using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core.Algorithms;

public class IterativePowerAlgorithm : IAlgorithm
{
    public string Id => "power.iterative";
    public string DisplayName => "Возведение в степень (итеративно)";
    public string Category => "Возведение в степень";
    public string TheoreticalComplexity => "O(n)";
    public ExperimentDefaults Defaults => new(
    NMax: 1_000, NStep: 10, MMax: 0, MStep: 0,
    RunsPerPoint: 1, Hypothesis: "Linear");

    public ExperimentDimension Dimension => ExperimentDimension.OneDimensional;
    public MeasureUnit MeasureUnit => MeasureUnit.Steps;

    public object GenerateInput(ExperimentPoint point, Random random)
    {
        // x — основание, n — степень. Возвращаем кортеж.
        return (X: 1.0001, N: point.N);
    }

    public void Execute(object input, IExecutionTracker tracker)
    {
        var (x, n) = ((double X, int N))input;
        double result = 1.0;
        for (int i = 0; i < n; i++)
        {
            result *= x;
            tracker.CountStep();   // каждое умножение — шаг
        }
        GC.KeepAlive(result);
    }
}