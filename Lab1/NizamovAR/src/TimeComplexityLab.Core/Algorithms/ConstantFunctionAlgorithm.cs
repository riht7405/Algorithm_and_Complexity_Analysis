using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core.Algorithms;

public class ConstantFunctionAlgorithm : IAlgorithm
{
    public string Id => "vector.constant";
    public string DisplayName => "Постоянная функция f(v) = 1";
    public string Category => "Векторы";
    public string TheoreticalComplexity => "O(1)";
    public ExperimentDefaults Defaults => new(
    NMax: 10_000, NStep: 1_000, MMax: 0, MStep: 0,
    RunsPerPoint: 5, Hypothesis: "Constant");

    public ExperimentDimension Dimension => ExperimentDimension.OneDimensional;
    public MeasureUnit MeasureUnit => MeasureUnit.Time;

    public object GenerateInput(ExperimentPoint point, Random random)
    {
        var v = new double[point.N];
        for (int i = 0; i < v.Length; i++) v[i] = random.NextDouble() * 100.0;
        return v;
    }

    public void Execute(object input, IExecutionTracker tracker)
    {
        double result = 1.0;
        GC.KeepAlive(result);
    }
}