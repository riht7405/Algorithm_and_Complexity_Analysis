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

    // Полное правило для генерации большого фрактала
    private static readonly string Temp =
        "wwwwwwwrrgwwwwwwwwwwwwwrrgggwwwwwwwwwwrrggbggwwwwwwwwwrrggwbbggwwwwwwwrr" +
        "rrrrrbbggwwwwwrrgbbbbbrrgggwwwrrggbggwrrggbggwrrggwbbggrggwbbggrrrrrrbbgrrrrrbb" +
        "ggbbbbbbbbbbbbbrrggbggwwwwwwwwwrrgg";

    private static readonly Dictionary<string, char> Rule1 = new();

    static SelmiFractal()
    {
        // Генерируем правило на основе Temp
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

        // Делаем начальную строку шире, чтобы фрактал было видно
        string curr = new string('w', 150) + "rrg" + new string('w', 150);
        var rows = new List<string>();

        for (int i = 0; i < p.Generations; i++)
        {
            rows.Add(curr);

            // Добавляем 'w' по краям, чтобы фрактал не упирался в границы
            curr = "www" + curr + "www";
            var next = new char[curr.Length - 6];

            for (int j = 0; j < next.Length; j++)
            {
                string key = curr.Substring(j, 7);
                // Если правило не найдено, используем 'w' (белый/фон)
                next[j] = Rule1.TryGetValue(key, out var val) ? val : 'w';
            }
            curr = new string(next);
        }

        int width = rows[0].Length;
        int height = rows.Count;
        var cells = new byte[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                cells[y * width + x] = rows[y][x] switch
                {
                    'w' => 0, // фон
                    'r' => 1, // красный
                    'g' => 2, // зеленый
                    'b' => 3, // синий
                    _ => 0
                };
            }
        }

        sw.Stop();

        return Task.FromResult<FractalResult>(new CellGridResult
        {
            FractalId = Id,
            ComputeTime = sw.Elapsed,
            Rows = height,
            Cols = width,
            States = 4,
            Cells = cells
        });
    }
}