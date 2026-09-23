using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core.Algorithms;

public class BubbleSortAlgorithm : IAlgorithm
{
    public string Id => "sorting.bubble";
    public string DisplayName => "Сортировка пузырьком";
    public string Category => "Сортировки";
    public string TheoreticalComplexity => "O(n²)";

    public ExperimentDimension Dimension => ExperimentDimension.OneDimensional;
    public MeasureUnit MeasureUnit => MeasureUnit.Time;

    public ExperimentDefaults Defaults => new(
        NMax: 25_000, NStep: 100, MMax: 0, MStep: 0,
        RunsPerPoint: 5, Hypothesis: "Quadratic");

    public object GenerateSource(ExperimentPoint maxPoint, Random random)
    {
        var v = new double[maxPoint.N];
        for (int i = 0; i < v.Length; i++) v[i] = random.NextDouble() * 1000.0;
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

    public object Clone(object input) => ((double[])input).Clone();

    public void Execute(object input, IExecutionTracker tracker)
    {
        var v = (double[])input;
        int n = v.Length;
        for (int i = 0; i < n - 1; i++)
            for (int j = 0; j < n - 1 - i; j++)
                if (v[j] > v[j + 1])
                    (v[j], v[j + 1]) = (v[j + 1], v[j]);
    }
}