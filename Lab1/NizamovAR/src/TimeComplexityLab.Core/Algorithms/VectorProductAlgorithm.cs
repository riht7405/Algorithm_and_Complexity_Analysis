using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core.Algorithms;

public class VectorProductAlgorithm : IAlgorithm
{
    public string Id => "vector.product";
    public string DisplayName => "Произведение элементов вектора";
    public string Category => "Векторы";
    public string TheoreticalComplexity => "O(n)";

    public ExperimentDimension Dimension => ExperimentDimension.OneDimensional;
    public MeasureUnit MeasureUnit => MeasureUnit.Time;

    public ExperimentDefaults Defaults => new(
        NMax: 30_000_000, NStep: 30_000, MMax: 0, MStep: 0,
        RunsPerPoint: 5, Hypothesis: "Linear");

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
        var v = (double[])input;
        double product = 1.0;
        for (int i = 0; i < v.Length; i++) product *= v[i];
        GC.KeepAlive(product);
    }
}