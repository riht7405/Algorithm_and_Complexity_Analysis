using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core;

public class VectorProductAlgorithm : IAlgorithm
{
    public string Id => "vector.product";
    public string DisplayName => "Произведение элементов вектора";
    public string Category => "Векторы";
    public string TheoreticalComplexity => "O(n)";

    public object GenerateInput(int n, Random random)
    {
        var v = new double[n];
        for (int i = 0; i < n; i++) v[i] = random.NextDouble() * 100.0;
        return v;
    }

    public void Execute(object input)
    {
        var v = (double[])input;
        double product = 1.0;
        for (int i = 0; i < v.Length; i++)
            product *= v[i];
        GC.KeepAlive(product);
    }
}