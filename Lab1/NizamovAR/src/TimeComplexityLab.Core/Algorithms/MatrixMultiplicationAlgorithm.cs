using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core.Algorithms;

public class MatrixMultiplicationAlgorithm : IAlgorithm
{
    public string Id => "matrix.multiply.classic";
    public string DisplayName => "Умножение матриц A(n×m) · B(m×n)";
    public string Category => "Матрицы";
    public string TheoreticalComplexity => "O(n²·m)";

    public ExperimentDimension Dimension => ExperimentDimension.TwoDimensional;
    public MeasureUnit MeasureUnit => MeasureUnit.Time;

    public ExperimentDefaults Defaults => new(
        NMax: 500, NStep: 50, MMax: 500, MStep: 50,
        RunsPerPoint: 1, Hypothesis: "Quadratic");

    public object GenerateSource(ExperimentPoint maxPoint, Random random)
    {
        int n = maxPoint.N;
        int m = maxPoint.M ?? n;

        var a = new double[n, m];
        var b = new double[m, n];

        for (int i = 0; i < n; i++)
            for (int j = 0; j < m; j++)
                a[i, j] = random.NextDouble() * 10.0;

        for (int i = 0; i < m; i++)
            for (int j = 0; j < n; j++)
                b[i, j] = random.NextDouble() * 10.0;

        return (a, b);
    }

    public object Slice(object source, ExperimentPoint point)
    {
        var (a, b) = ((double[,] A, double[,] B))source;
        int n = point.N;
        int m = point.M ?? n;

        var aSlice = new double[n, m];
        var bSlice = new double[m, n];

        for (int i = 0; i < n; i++)
            for (int j = 0; j < m; j++)
                aSlice[i, j] = a[i, j];

        for (int i = 0; i < m; i++)
            for (int j = 0; j < n; j++)
                bSlice[i, j] = b[i, j];

        return (aSlice, bSlice);
    }

    public object Clone(object input) => input;

    public void Execute(object input, IExecutionTracker tracker)
    {
        var (a, b) = ((double[,] A, double[,] B))input;
        int n = a.GetLength(0);
        int m = a.GetLength(1);
        int p = b.GetLength(1);

        var c = new double[n, p];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < p; j++)
            {
                double sum = 0.0;
                for (int k = 0; k < m; k++) sum += a[i, k] * b[k, j];
                c[i, j] = sum;
            }
        GC.KeepAlive(c);
    }
}