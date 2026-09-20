using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core;

public class PolynomialHornerAlgorithm : IAlgorithm
{
    private const double X = 1.5;

    public string Id => "polynomial.horner";
    public string DisplayName => "Многочлен (метод Горнера)";
    public string Category => "Многочлены";
    public string TheoreticalComplexity => "O(n)";

    public object GenerateInput(int n, Random random)
    {
        var v = new double[n];
        for (int i = 0; i < n; i++) v[i] = random.NextDouble() * 2.0 - 1.0;
        return v;
    }

    public void Execute(object input)
    {
        var v = (double[])input;
        if (v.Length == 0) return;

        double result = v[v.Length - 1];
        for (int k = v.Length - 2; k >= 0; k--)
            result = result * X + v[k];
        GC.KeepAlive(result);
    }
}