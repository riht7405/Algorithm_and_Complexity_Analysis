using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core.Algorithms;

public class DtwAlgorithm : IAlgorithm
{
    public string Id => "sequence.dtw";
    public string DisplayName => "Dynamic Time Warping";
    public string Category => "Последовательности";
    public string TheoreticalComplexity => "O(n·m)";
    public ExperimentDefaults Defaults => new(
    NMax: 500, NStep: 25, MMax: 500, MStep: 25,
    RunsPerPoint: 1, Hypothesis: "Quadratic");

    public ExperimentDimension Dimension => ExperimentDimension.TwoDimensional;
    public MeasureUnit MeasureUnit => MeasureUnit.Time;

    public object GenerateInput(ExperimentPoint point, Random random)
    {
        int n = point.N;
        int m = point.M ?? n;

        var a = new double[n];
        var b = new double[m];
        for (int i = 0; i < n; i++) a[i] = random.NextDouble() * 100.0;
        for (int j = 0; j < m; j++) b[j] = random.NextDouble() * 100.0;

        return (a, b);
    }

    public void Execute(object input, IExecutionTracker tracker)
    {
        var (a, b) = ((double[] A, double[] B))input;
        int n = a.Length;
        int m = b.Length;

        // Две строки вместо полной таблицы — O(min(n,m)) памяти
        var prev = new double[m + 1];
        var curr = new double[m + 1];

        for (int j = 1; j <= m; j++) prev[j] = double.PositiveInfinity;

        for (int i = 1; i <= n; i++)
        {
            curr[0] = double.PositiveInfinity;

            for (int j = 1; j <= m; j++)
            {
                double cost = Math.Abs(a[i - 1] - b[j - 1]);

                double best = Math.Min(
                    Math.Min(prev[j], curr[j - 1]),
                    prev[j - 1]);

                if (i == 1 && j == 1) best = 0;
                curr[j] = cost + best;
            }

            (prev, curr) = (curr, prev);
        }

        GC.KeepAlive(prev[m]);
    }
}