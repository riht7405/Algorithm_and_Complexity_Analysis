using System.Diagnostics;
using FractalPlatform.Domain.Abstractions;

namespace FractalPlatform.Domain.Fractals;

public class HofstadterFractal : IFractal
{
    public string Id => "hofstadter";
    public FractalMetadata Metadata { get; } = new("Hofstadter Butterfly", FractalKind.Spectral, "Spectrum", IsHeavy: true);

    public Task<FractalResult> ComputeAsync(FractalRequest request, CancellationToken ct = default)
    {
        var p = HofstadterParams.FromJson(request.Parameters);
        var sw = Stopwatch.StartNew();
        var density = new float[p.Width * p.Height];

        for (int q = 1; q <= p.MaxQ; q++)
        {
            for (int pNum = 1; pNum < q; pNum++)
            {
                if (GCD(pNum, q) != 1) continue;
                double alpha = (double)pNum / q;
                int x = (int)(alpha * (p.Width - 1));

                var diag = new double[q];
                var offDiag = new double[q - 1];
                for (int i = 0; i < q; i++)
                {
                    diag[i] = 2.0 * Math.Cos(2.0 * Math.PI * alpha * i);
                    if (i < q - 1) offDiag[i] = 1.0;
                }

                // Запускаем рекурсивный поиск (передаем начальную глубину 0)
                var energies = new List<double>();
                FindRootsRecursive(diag, offDiag, -4.0, 4.0, 0.001, 0, energies);

                foreach (var e in energies)
                {
                    double norm = (e + 4.0) / 8.0;
                    int y = (int)((1.0 - norm) * (p.Height - 1));
                    if (y >= 0 && y < p.Height)
                        density[y * p.Width + x] += 1.0f;
                }
            }
        }

        float max = 1.0f;
        for (int i = 0; i < density.Length; i++) if (density[i] > max) max = density[i];
        for (int i = 0; i < density.Length; i++) density[i] /= max;

        sw.Stop();
        return Task.FromResult<FractalResult>(new SpectrumResult
        {
            FractalId = Id,
            ComputeTime = sw.Elapsed,
            Width = p.Width,
            Height = p.Height,
            Density = density
        });
    }

    // Считаем количество корней меньше E (Свойство Штурма)
    private static int CountRoots(double[] diag, double[] offDiag, double E)
    {
        int count = 0;
        double pPrev = 1.0; // P_0
        double pCurr = diag[0] - E; // P_1

        if (pCurr < 0) count++;

        for (int i = 1; i < diag.Length; i++)
        {
            double pNext = (diag[i] - E) * pCurr - offDiag[i - 1] * offDiag[i - 1] * pPrev;

            // Защита от точного нуля
            if (pNext == 0) pNext = 1e-15;

            if (pNext * pCurr < 0) count++;

            pPrev = pCurr;
            pCurr = pNext;
        }
        return count;
    }

    // РЕКУРСИВНЫЙ поиск корней (с защитой от переполнения стека)
    private static void FindRootsRecursive(double[] diag, double[] offDiag, double minE, double maxE, double eps, int depth, List<double> results)
    {
        // 1. Защита от бесконечной рекурсии (лимит глубины и минимальный размер отрезка)
        if (depth > 40 || (maxE - minE) < 1e-12) return;

        int rootsInRange = CountRoots(diag, offDiag, maxE) - CountRoots(diag, offDiag, minE);

        if (rootsInRange <= 0) return;

        if (rootsInRange == 1 && (maxE - minE) < eps)
        {
            results.Add((minE + maxE) / 2.0);
            return;
        }

        double mid = (minE + maxE) / 2.0;

        // 2. Защита от схлопывания отрезка из-за точности double
        if (mid <= minE || mid >= maxE) return;

        FindRootsRecursive(diag, offDiag, minE, mid, eps, depth + 1, results);
        FindRootsRecursive(diag, offDiag, mid, maxE, eps, depth + 1, results);
    }

    private static int GCD(int a, int b) => b == 0 ? a : GCD(b, a % b);
}