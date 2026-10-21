using System.Diagnostics;
using FractalPlatform.Domain.Abstractions;

namespace FractalPlatform.Domain.Fractals;

public class KuvinaFractal : IFractal
{
    public string Id => "kuvina";

    public FractalMetadata Metadata { get; } = new(
        "Kuvina Triangle",
        FractalKind.Geometric, // или Ifs
        "CellGrid",
        IsHeavy: false,
        CacheTtl: TimeSpan.FromMinutes(30)
    );

    public Task<FractalResult> ComputeAsync(FractalRequest request, CancellationToken ct = default)
    {
        var p = KuvinaParams.FromJson(request.Parameters);

        var sw = Stopwatch.StartNew();

        // Ширина сетки в 2 раза больше поколений (для равностороннего треугольника)
        int cols = 2 * p.Generations + 1;
        int rows = p.Generations;
        var grid = new byte[rows * cols];

        // Центральная вершина
        int startC = p.Generations;
        grid[0 * cols + startC] = 1;

        for (int r = 1; r < rows; r++)
        {
            for (int c = 1; c < cols - 1; c++)
            {
                // Берем три значения сверху: слева, по центру, справа
                int left = grid[(r - 1) * cols + c - 1];
                int center = grid[(r - 1) * cols + c];
                int right = grid[(r - 1) * cols + c + 1];

                int sum = p.Variation.ToLowerInvariant() switch
                {
                    "bi" => left + right,
                    "shadow" => left - center + right,
                    "biskew" => left + center,
                    "shadowskew" => left + center - right,
                    _ => left + center + right // Normal
                };

                // Остаток по модулю (и защита от отрицательных чисел в C#)
                int val = sum % p.Modulus;
                if (val < 0) val += p.Modulus;

                grid[r * cols + c] = (byte)val;
            }
        }

        sw.Stop();

        var result = new CellGridResult
        {
            FractalId = Id,
            ComputeTime = sw.Elapsed,
            Rows = rows,
            Cols = cols,
            States = p.Modulus,
            Cells = grid
        };

        return Task.FromResult<FractalResult>(result);
    }
}