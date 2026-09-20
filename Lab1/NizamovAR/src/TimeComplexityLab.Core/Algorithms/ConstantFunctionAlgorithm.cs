using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core;

public class ConstantFunctionAlgorithm : IAlgorithm
{
    public string Id => "vector.constant";
    public string DisplayName => "Постоянная функция f(v) = 1";
    public string Category => "Векторы";
    public string TheoreticalComplexity => "O(1)";

    public object GenerateInput(int n, Random random)
    {
        var v = new double[n];
        for (int i = 0; i < n; i++) v[i] = random.NextDouble() * 100.0;
        return v;
    }

    public void Execute(object input)
    {
        // O(1): не трогаем массив вообще. Возвращаем константу.
        double result = Double.MaxValue;
        GC.KeepAlive(result);
    }
}