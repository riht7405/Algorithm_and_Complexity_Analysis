using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core.Algorithms;

public class GnomeSortAlgorithm : IAlgorithm
{
    public string Id => "sorting.gnome";
    public string DisplayName => "Гномья сортировка";
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
        if (v == null || v.Length <= 1) return;

        int i = 1;
        int n = v.Length;

        while (i < n)
        {
            if (i == 0 || v[i - 1] <= v[i])
            {
                i++;
            }
            else
            {
                double temp = v[i];
                v[i] = v[i - 1];
                v[i - 1] = temp;

                i--;
            }
        }
    }
}