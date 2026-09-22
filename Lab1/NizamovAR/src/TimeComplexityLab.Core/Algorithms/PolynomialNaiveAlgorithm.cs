using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core.Algorithms;

public class PolynomialNaiveAlgorithm : IAlgorithm
{
    private const double X = 1.5;

    public string Id => "polynomial.naive";
    public string DisplayName => "Многочлен (наивный метод)";
    public string Category => "Многочлены";
    public string TheoreticalComplexity => "O(n²)";
    public ExperimentDefaults Defaults => new(
    NMax: 5_000, NStep: 100, MMax: 0, MStep: 0,
    RunsPerPoint: 5, Hypothesis: "Quadratic");

    public ExperimentDimension Dimension => ExperimentDimension.OneDimensional;
    public MeasureUnit MeasureUnit => MeasureUnit.Time;

    public object GenerateInput(ExperimentPoint point, Random random)
    {
        var v = new double[point.N];
        for (int i = 0; i < v.Length; i++) v[i] = random.NextDouble() * 2.0 - 1.0;
        return v;
    }

    public void Execute(object input, IExecutionTracker tracker)
    {
        var v = (double[])input;
        double result = 0.0;
        for (int k = 0; k < v.Length; k++)
        {
            double power = 1.0;
            for (int j = 0; j < k; j++) power *= X;
            result += v[k] * power;
        }
        GC.KeepAlive(result);
    }
}