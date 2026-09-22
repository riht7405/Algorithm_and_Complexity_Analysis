using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core.Algorithms;

public class QuickSortAlgorithm : IAlgorithm
{
    public string Id => "sorting.quick";
    public string DisplayName => "Быстрая сортировка";
    public string Category => "Сортировки";
    public string TheoreticalComplexity => "O(n log n)";
    public ExperimentDefaults Defaults => new(
    NMax: 100_000, NStep: 1_000, MMax: 0, MStep: 0,
    RunsPerPoint: 5, Hypothesis: "NLogN");

    public ExperimentDimension Dimension => ExperimentDimension.OneDimensional;
    public MeasureUnit MeasureUnit => MeasureUnit.Time;

    public object GenerateInput(ExperimentPoint point, Random random)
    {
        var v = new double[point.N];
        for (int i = 0; i < v.Length; i++) v[i] = random.NextDouble() * 1000.0;
        return v;
    }

    public void Execute(object input, IExecutionTracker tracker)
    {
        var v = (double[])input;
        Sort(v, 0, v.Length - 1);
    }

    private static void Sort(double[] v, int low, int high)
    {
        if (low >= high) return;
        int p = Partition(v, low, high);
        Sort(v, low, p - 1);
        Sort(v, p + 1, high);
    }

    private static int Partition(double[] v, int low, int high)
    {
        double pivot = v[high];
        int i = low - 1;
        for (int j = low; j < high; j++)
        {
            if (v[j] < pivot)
            {
                i++;
                (v[i], v[j]) = (v[j], v[i]);
            }
        }
        (v[i + 1], v[high]) = (v[high], v[i + 1]);
        return i + 1;
    }
}