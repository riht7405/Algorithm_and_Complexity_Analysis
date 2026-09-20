using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core;

public class BubbleSortAlgorithm : IAlgorithm
{
    public string Id => "sorting.bubble";
    public string DisplayName => "Сортировка пузырьком";
    public string Category => "Сортировки";
    public string TheoreticalComplexity => "O(n²)";

    public object GenerateInput(int n, Random random)
    {
        var v = new double[n];
        for (int i = 0; i < n; i++) v[i] = random.NextDouble() * 1000.0;
        return v;
    }

    public void Execute(object input)
    {
        var v = (double[])input;
        int n = v.Length;
        for (int i = 0; i < n - 1; i++)
        {
            for (int j = 0; j < n - 1 - i; j++)
            {
                if (v[j] > v[j + 1])
                {
                    double tmp = v[j];
                    v[j] = v[j + 1];
                    v[j + 1] = tmp;
                }
            }
        }
    }
}