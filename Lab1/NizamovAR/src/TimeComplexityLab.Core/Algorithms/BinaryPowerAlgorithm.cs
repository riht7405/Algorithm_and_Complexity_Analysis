using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core.Algorithms;

public class BinaryPowerAlgorithm : IAlgorithm
{
    public string Id => "power.binary";
    public string DisplayName => "Возведение в степень (бинарное)";
    public string Category => "Возведение в степень";
    public string TheoreticalComplexity => "O(log n)";

    public ExperimentDimension Dimension => ExperimentDimension.OneDimensional;
    public MeasureUnit MeasureUnit => MeasureUnit.Steps;

    public ExperimentDefaults Defaults => new(
        NMax: 10_000, NStep: 5, MMax: 0, MStep: 0,
        RunsPerPoint: 5, Hypothesis: "Logarithmic");

    public object GenerateSource(ExperimentPoint maxPoint, Random random)
        => (X: 1.0001, MaxN: maxPoint.N);

    public object Slice(object source, ExperimentPoint point)
        => (X: 1.0001, N: point.N);

    public object Clone(object input) => input;

    public void Execute(object input, IExecutionTracker tracker)
    {
        var (x, n) = ((double X, int N))input;

        if (n == 0) { GC.KeepAlive(1.0); return; }

        double result = 1.0;
        double b = x;
        int exp = n;

        while (exp > 0)
        {
            if ((exp & 1) == 1)
            {
                result *= b;
                tracker.CountStep();
            }
            exp >>= 1;
            if (exp > 0)
            {
                b *= b;
                tracker.CountStep();
            }
        }
        GC.KeepAlive(result);
    }
}