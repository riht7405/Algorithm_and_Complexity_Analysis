using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core;

public class QuickSortAlgorithm : IAlgorithm
{
    public string Id => "sorting.quick";
    public string DisplayName => "Быстрая сортировка";
    public string Category => "Сортировки";
    public string TheoreticalComplexity => "O(n log n)";

    public object GenerateInput(int n, Random random)
    {
        var v = new double[n];
        for (int i = 0; i < n; i++) v[i] = random.NextDouble() * 1000.0;
        return v;
    }

    public void Execute(object input)
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