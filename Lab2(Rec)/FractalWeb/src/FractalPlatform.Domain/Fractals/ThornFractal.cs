using System.Diagnostics;
using FractalPlatform.Domain.Abstractions;

namespace FractalPlatform.Domain.Fractals;

public class ThornFractal : IFractal
{
    public string Id => "thorn";

    public FractalMetadata Metadata { get; } = new(
        "Thorn Fractal (Secant Sea)",
        FractalKind.EscapeTime,
        "IterationMap",
        IsHeavy: false,
        CacheTtl: TimeSpan.FromMinutes(5)
    );

    public Task<FractalResult> ComputeAsync(FractalRequest request, CancellationToken ct = default)
    {
        // 1. Достаем параметры из запроса
        var p = ThornParams.FromJson(request.Parameters);

        var sw = Stopwatch.StartNew();
        var iterations = new ushort[p.Width * p.Height];
        double dx = (p.XMax - p.XMin) / p.Width;
        double dy = (p.YMax - p.YMin) / p.Height;

        // 2. Считаем с использованием p.* переменных
        Parallel.For(0, p.Height, new ParallelOptions { CancellationToken = ct }, y =>
        {
            double y0 = p.YMin + y * dy;
            for (int x = 0; x < p.Width; x++)
            {
                double x0 = p.XMin + x * dx;
                double zx = x0, zy = y0;
                int iter = 0;

                for (int i = 0; i < p.MaxIter; i++)
                {
                    double cosZy = Math.Cos(zy);
                    double sinZx = Math.Sin(zx);

                    if (Math.Abs(cosZy) < 1e-10 || Math.Abs(sinZx) < 1e-10)
                    {
                        iter = p.MaxIter;
                        break;
                    }

                    double newZx = zx / cosZy + p.Cx;
                    double newZy = zy / sinZx + p.Cy;
                    zx = newZx; zy = newZy;

                    if (zx * zx + zy * zy > p.Bailout * p.Bailout)
                    {
                        iter = i;
                        break;
                    }
                    iter = i;
                }
                iterations[y * p.Width + x] = (ushort)Math.Min(iter, ushort.MaxValue);
            }
        });

        sw.Stop();

        // 3. Возвращаем результат с правильными метаданными
        var result = new IterationMapResult
        {
            FractalId = Id,
            ComputeTime = sw.Elapsed,
            Width = p.Width,
            Height = p.Height,
            MaxIterations = p.MaxIter,
            Iterations = iterations,
            XMin = p.XMin,
            XMax = p.XMax,
            YMin = p.YMin,
            YMax = p.YMax
        };

        return Task.FromResult<FractalResult>(result);
    }
}