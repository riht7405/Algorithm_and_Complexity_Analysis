using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core;

public class VectorSumAlgorithm : IAlgorithm
{
    public string Id => "vector.sum";
    public string DisplayName => "Сумма элементов вектора";
    public string Category => "Векторы";
    public string TheoreticalComplexity => "O(n)";

    public object GenerateInput(int n, Random random)
    {
        var vector = new double[n];
        for (int i = 0; i < n; i++)
            vector[i] = random.NextDouble() * 100.0;
        return vector;
    }

    public void Execute(object input)
    {
        var vector = (double[])input;
        double sum = 0.0;
        for (int i = 0; i < vector.Length; i++)
            sum += vector[i];

        // Защита от выкидывания цикла JIT-компилятором в Release
        GC.KeepAlive(sum);
    }
}