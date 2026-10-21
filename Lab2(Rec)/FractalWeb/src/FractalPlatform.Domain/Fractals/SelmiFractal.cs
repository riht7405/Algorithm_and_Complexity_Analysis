using System.Diagnostics;
using FractalPlatform.Domain.Abstractions;

namespace FractalPlatform.Domain.Fractals;

public class SelmiFractal : IFractal
{
    public string Id => "selmi";

    public FractalMetadata Metadata { get; } = new(
        "Selmi Triangle",
        FractalKind.Geometric,
        "CellGrid",
        IsHeavy: false,
        CacheTtl: TimeSpan.FromMinutes(30)
    );

    private static readonly string Temp =
        "wwwwwwwrrgwwwwwwwwwwwwwrrgggwwwwwwwwwwrrggbggwwwwwwwwwrrggwbbggwwwwwwwrr" +
        "rrrrrbbggwwwwwrrgbbbbbrrgggwwwrrggbggwrrggbggwrrggwbbggrggwbbggrrrrrrbbgrrrrrbb" +
        "ggbbbbbbbbbbbbbrrggbggwwwwwwwwwrrgg";

    private static readonly Dictionary<string, char> Rule1 = new();

    static SelmiFractal()
    {
        for (int i = 0; i <= Temp.Length - 7 - 20; i++)
        {
            string key = Temp.Substring(i, 7);
            if (!Rule1.ContainsKey(key))
                Rule1[key] = Temp[i + 20];
        }
    }

    public Task<FractalResult> ComputeAsync(FractalRequest request, CancellationToken ct = default)
    {
        var p = SelmiParams.FromJson(request.Parameters);
        var sw = Stopwatch.StartNew();

        // Делаем ширину зависящей от поколений (примерно 2:1), чтобы фрактал был квадратным
        int width = Math.Max(94, p.Generations * 2);
        int center = width / 2;

        // Центрируем начальное "rrg"
        string curr = new string('w', center - 1) + "rrg" + new string('w', width - center - 2);
        var rows = new List<string>();

        for (int i = 0; i < p.Generations; i++)
        {
            rows.Add(curr);
            curr = "www" + curr + "www";
            var next = new char[curr.Length - 6];
            for (int j = 0; j < next.Length; j++)
            {
                string key = curr.Substring(j, 7);
                next[j] = Rule1.TryGetValue(key, out var val) ? val : 'w';
            }
            curr = new string(next);
        }

        int height = rows.Count;
        var cells = new byte[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                cells[y * width + x] = rows[y][x] switch
                {
                    'w' => 0,
                    'r' => 1,
                    'g' => 2,
                    'b' => 3,
                    _ => 0
                };
            }
        }

        sw.Stop();

        var result = new CellGridResult
        {
            FractalId = Id,
            ComputeTime = sw.Elapsed,
            Rows = height,
            Cols = width,
            States = 4,
            Cells = cells
        };

        return Task.FromResult<FractalResult>(result);
    }
}