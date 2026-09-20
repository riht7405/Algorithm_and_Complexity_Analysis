using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core;

public class PolynomialNaiveAlgorithm : IAlgorithm
{
    private const double X = 1.5;

    public string Id => "polynomial.naive";
    public string DisplayName => "Многочлен (наивный метод)";
    public string Category => "Многочлены";
    public string TheoreticalComplexity => "O(n²)";

    public object GenerateInput(int n, Random random)
    {
        var v = new double[n];
        for (int i = 0; i < n; i++) v[i] = random.NextDouble() * 2.0 - 1.0;
        return v;
    }

    public void Execute(object input)
    {
        var v = (double[])input;
        double result = 0.0;
        for (int k = 0; k < v.Length; k++)
        {
            double power = 1.0;
            for (int j = 0; j < k; j++) power *= X;
            result += v[k] * power;
        }
        GC.KeepAlive(result);
    }
}