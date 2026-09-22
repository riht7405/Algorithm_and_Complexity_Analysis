using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core.Algorithms;

public class VectorProductAlgorithm : IAlgorithm
{
    public string Id => "vector.product";
    public string DisplayName => "Произведение элементов вектора";
    public string Category => "Векторы";
    public string TheoreticalComplexity => "O(n)";
    public ExperimentDefaults Defaults => new(
    NMax: 50_000, NStep: 500, MMax: 0, MStep: 0,
    RunsPerPoint: 5, Hypothesis: "Linear");

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
        var v = (double[])input;
        double product = 1.0;
        for (int i = 0; i < v.Length; i++)
            product *= v[i];
        GC.KeepAlive(product);
    }
}