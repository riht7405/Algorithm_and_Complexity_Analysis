using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core.Algorithms;

public class BinaryPowerAlgorithm : IAlgorithm
{
    public string Id => "power.binary";
    public string DisplayName => "Возведение в степень (бинарное)";
    public string Category => "Возведение в степень";
    public string TheoreticalComplexity => "O(log n)";
    public ExperimentDefaults Defaults => new(
    NMax: 1_000, NStep: 10, MMax: 0, MStep: 0,
    RunsPerPoint: 1, Hypothesis: "Logarithmic");

    public ExperimentDimension Dimension => ExperimentDimension.OneDimensional;
    public MeasureUnit MeasureUnit => MeasureUnit.Steps;

    public object GenerateInput(ExperimentPoint point, Random random)
    {
        return (X: 1.0001, N: point.N);
    }

    public void Execute(object input, IExecutionTracker tracker)
    {
        var (x, n) = ((double X, int N))input;
        double result = 1.0;
        double base_ = x;
        int exp = n;

        while (exp > 0)
        {
            tracker.CountStep();   // каждое умножение = шаг
            if ((exp & 1) == 1)
                result *= base_;
            base_ *= base_;
            exp >>= 1;
        }

        GC.KeepAlive(result);
    }
}