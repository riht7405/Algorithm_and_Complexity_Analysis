using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core.Algorithms;

public class HirschbergAlgorithm : IAlgorithm
{
    public string Id => "sequence.hirschberg";
    public string DisplayName => "Алгоритм Хиршберга (LCS)";
    public string Category => "Последовательности";
    public string TheoreticalComplexity => "O(n·m)";
    public ExperimentDefaults Defaults => new(
    NMax: 1_000, NStep: 50, MMax: 1_000, MStep: 50,
    RunsPerPoint: 1, Hypothesis: "Quadratic");

    public ExperimentDimension Dimension => ExperimentDimension.TwoDimensional;
    public MeasureUnit MeasureUnit => MeasureUnit.Time;

    public object GenerateInput(ExperimentPoint point, Random random)
    {
        int n = point.N;
        int m = point.M ?? n;

        const string alphabet = "ACGT";
        var sx = new char[n];
        var sy = new char[m];
        for (int i = 0; i < n; i++) sx[i] = alphabet[random.Next(alphabet.Length)];
        for (int j = 0; j < m; j++) sy[j] = alphabet[random.Next(alphabet.Length)];

        return (new string(sx), new string(sy));
    }

    public void Execute(object input, IExecutionTracker tracker)
    {
        var (a, b) = ((string A, string B))input;
        var result = LcsRecursive(a, 0, a.Length, b, 0, b.Length, tracker);
        GC.KeepAlive(result);
    }

    private static string LcsRecursive(
        string a, int aStart, int aLen,
        string b, int bStart, int bLen,
        IExecutionTracker tracker)
    {
        if (aLen == 0 || bLen == 0) return string.Empty;

        if (aLen == 1)
        {
            char target = a[aStart];
            for (int j = 0; j < bLen; j++)
            {
                tracker.CountStep();
                if (b[bStart + j] == target) return target.ToString();
            }
            return string.Empty;
        }

        int aMid = aLen / 2;
        int bMid = FindSplit(a, aStart, aMid, aLen, b, bStart, bLen, tracker);

        var left = LcsRecursive(a, aStart, aMid, b, bStart, bMid, tracker);
        var right = LcsRecursive(a, aStart + aMid, aLen - aMid,
                                 b, bStart + bMid, bLen - bMid, tracker);

        return left + right;
    }

    private static int FindSplit(
        string a, int aStart, int aMid, int aLen,
        string b, int bStart, int bLen,
        IExecutionTracker tracker)
    {
        var l1 = ComputeRow(a, aStart, aMid, b, bStart, bLen, tracker);
        var l2 = ComputeRow(a, aStart + aMid, aLen - aMid, b, bStart, bLen, tracker);

        int best = 0, bestSum = -1;
        for (int j = 0; j <= bLen; j++)
        {
            int sum = l1[j] + l2[bLen - j];
            if (sum > bestSum)
            {
                bestSum = sum;
                best = j;
            }
        }
        return best;
    }

    private static int[] ComputeRow(
        string a, int aStart, int aLen,
        string b, int bStart, int bLen,
        IExecutionTracker tracker)
    {
        var prev = new int[bLen + 1];
        var curr = new int[bLen + 1];

        for (int i = 1; i <= aLen; i++)
        {
            for (int j = 1; j <= bLen; j++)
            {
                tracker.CountStep();
                if (a[aStart + i - 1] == b[bStart + j - 1])
                    curr[j] = prev[j - 1] + 1;
                else
                    curr[j] = Math.Max(prev[j], curr[j - 1]);
            }
            (prev, curr) = (curr, prev);
            Array.Clear(curr);
        }

        return prev;
    }
}