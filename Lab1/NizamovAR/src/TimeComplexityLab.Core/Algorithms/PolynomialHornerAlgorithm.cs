using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core.Algorithms;

public class PolynomialHornerAlgorithm : IAlgorithm
{
    private const double X = 1.5;

    public string Id => "polynomial.horner";
    public string DisplayName => "Многочлен (метод Горнера)";
    public string Category => "Многочлены";
    public string TheoreticalComplexity => "O(n)";

    public ExperimentDimension Dimension => ExperimentDimension.OneDimensional;
    public MeasureUnit MeasureUnit => MeasureUnit.Time;

    public ExperimentDefaults Defaults => new(
        NMax: 30_000_000, NStep: 30_000, MMax: 0, MStep: 0,
        RunsPerPoint: 5, Hypothesis: "Linear");

    public object GenerateSource(ExperimentPoint maxPoint, Random random)
    {
        var v = new double[maxPoint.N];
        for (int i = 0; i < v.Length; i++) v[i] = random.NextDouble() * 2.0 - 1.0;
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

    public object Clone(object input) => input;

    public void Execute(object input, IExecutionTracker tracker)
    {
        var v = (double[])input;
        if (v.Length == 0) return;

        double result = v[v.Length - 1];
        for (int k = v.Length - 2; k >= 0; k--)
            result = result * X + v[k];
        GC.KeepAlive(result);
    }
}