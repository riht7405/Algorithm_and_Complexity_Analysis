using System.Text;
using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core.Algorithms;

public class HirschbergAlgorithm : IAlgorithm
{
    public string Id => "lcs.hirschberg";
    public string DisplayName => "Алгоритм Хиршберга (Memory Optimized)";
    public string Category => "Динамическое программирование";
    public string TheoreticalComplexity => "O(n²)";

    // Генерируем пару случайных строк длины n (например, из ДНК-алфавита A, C, G, T)
    public object GenerateInput(int n, Random random)
    {
        const string alphabet = "ACGT";
        var sbX = new StringBuilder(n);
        var sbY = new StringBuilder(n);

        for (int i = 0; i < n; i++)
        {
            sbX.Append(alphabet[random.Next(alphabet.Length)]);
            sbY.Append(alphabet[random.Next(alphabet.Length)]);
        }

        return (sbX.ToString(), sbY.ToString());
    }

    public void Execute(object input)
    {
        var (x, y) = ((string, string))input;

        FindLCS(x, y);
    }

    private static string FindLCS(string x, string y)
    {
        if (string.IsNullOrEmpty(x) || string.IsNullOrEmpty(y)) return "";

        StringBuilder result = new StringBuilder();
        BuildLCS(x.AsSpan(), y.AsSpan(), result);
        return result.ToString();
    }

    private static void BuildLCS(ReadOnlySpan<char> x, ReadOnlySpan<char> y, StringBuilder sb)
    {
        if (x.IsEmpty || y.IsEmpty) return;

        if (x.Length == 1)
        {
            if (y.IndexOf(x[0]) != -1)
            {
                sb.Append(x[0]);
            }
            return;
        }

        int midX = x.Length / 2;

        ReadOnlySpan<char> xLeft = x.Slice(0, midX);
        ReadOnlySpan<char> xRight = x.Slice(midX);

        int[] scoreLeft = GetLCSProfileForward(xLeft, y);
        int[] scoreRight = GetLCSProfileBackward(xRight, y);

        int maxLen = -1;
        int splitY = 0;
        int n = y.Length;

        for (int j = 0; j <= n; j++)
        {
            int currentLen = scoreLeft[j] + scoreRight[n - j];
            if (currentLen > maxLen)
            {
                maxLen = currentLen;
                splitY = j;
            }
        }

        BuildLCS(xLeft, y.Slice(0, splitY), sb);
        BuildLCS(xRight, y.Slice(splitY), sb);
    }

    private static int[] GetLCSProfileForward(ReadOnlySpan<char> x, ReadOnlySpan<char> y)
    {
        int n = y.Length;
        int[] previousRow = new int[n + 1];
        int[] currentRow = new int[n + 1];

        for (int i = 1; i <= x.Length; i++)
        {
            Array.Copy(currentRow, previousRow, currentRow.Length);
            for (int j = 1; j <= n; j++)
            {
                if (x[i - 1] == y[j - 1])
                    currentRow[j] = previousRow[j - 1] + 1;
                else
                    currentRow[j] = Math.Max(previousRow[j], currentRow[j - 1]);
            }
        }
        return currentRow;
    }

    private static int[] GetLCSProfileBackward(ReadOnlySpan<char> x, ReadOnlySpan<char> y)
    {
        int n = y.Length;
        int[] previousRow = new int[n + 1];
        int[] currentRow = new int[n + 1];

        for (int i = x.Length - 1; i >= 0; i--)
        {
            Array.Copy(currentRow, previousRow, currentRow.Length);
            for (int j = n - 1; j >= 0; j--)
            {
                int targetJ = n - j;

                if (x[i] == y[j])
                    currentRow[targetJ] = previousRow[targetJ - 1] + 1;
                else
                    currentRow[targetJ] = Math.Max(previousRow[targetJ], currentRow[targetJ - 1]);
            }
        }
        return currentRow;
    }
}