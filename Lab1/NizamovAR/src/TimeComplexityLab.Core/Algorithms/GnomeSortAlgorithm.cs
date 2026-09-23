using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core.Algorithms;

public class GnomeSortAlgorithm : IAlgorithm
{
    public string Id => "sorting.gnome";
    public string DisplayName => "Гномья сортировка";
    public string Category => "Сортировки";
    public string TheoreticalComplexity => "O(n²)";

    public ExperimentDimension Dimension => ExperimentDimension.OneDimensional;
    public MeasureUnit MeasureUnit => MeasureUnit.Time;

    public ExperimentDefaults Defaults => new(
        NMax: 25_000, NStep: 500, MMax: 0, MStep: 0,
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
        int i = 0;
        while (i < v.Length)
        {
            if (i == 0 || v[i - 1] <= v[i]) i++;
            else
            {
                (v[i - 1], v[i]) = (v[i], v[i - 1]);
                i--;
            }
        }
    }
}