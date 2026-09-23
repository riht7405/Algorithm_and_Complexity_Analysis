using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core.Algorithms;

public class ConstantFunctionAlgorithm : IAlgorithm
{
    public string Id => "vector.constant";
    public string DisplayName => "Постоянная функция f(v) = 1";
    public string Category => "Векторы";
    public string TheoreticalComplexity => "O(1)";

    public ExperimentDimension Dimension => ExperimentDimension.OneDimensional;
    public MeasureUnit MeasureUnit => MeasureUnit.Time;

    public ExperimentDefaults Defaults => new(
        NMax: 10_000_000, NStep: 1000, MMax: 0, MStep: 0,
        RunsPerPoint: 10, Hypothesis: "Constant");

    public object GenerateSource(ExperimentPoint maxPoint, Random random)
    {
        var v = new double[maxPoint.N];
        for (int i = 0; i < v.Length; i++) v[i] = random.NextDouble() * 100.0;
        return v;
    }

    public object Slice(object source, ExperimentPoint point)
    {
        var v = (double[])source;
        if (point.N >= v.Length) return v;

        var slice = new double[point.N];
        Array.Copy(v, slice, point.N);
        return slice;
    }

    public object Clone(object input) => input;

    public void Execute(object input, IExecutionTracker tracker)
    {
        double result = 1.0;
        GC.KeepAlive(result);
    }
}