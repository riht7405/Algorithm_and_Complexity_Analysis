using LabApp.Api;
using LabApp.Core;
using LabApp.Core.Texts;
using LabApp.Core.WordSort;

namespace LabApp.Api;

/// <summary>
/// API-маршруты задания 3: готовые тексты, случайный текст, алгоритмы сортировки слов,
/// запуск сортировки с подсчётом вхождений и замеры времени.
/// Новый маршрут добавляется в этот класс.
/// </summary>
public static class WordSortEndpoints
{
    public static void MapWordSortEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        // Готовые тексты (5…100 слов создаются при первом обращении) + свои файлы из Data/texts.
        // Устаревшие сгенерированные файлы большого объёма в список демонстрации не попадают.
        api.MapGet("/texts", (ITextStore store) =>
        {
            store.EnsureDefaults();
            return Results.Ok(store.ListTexts()
                .Where(t => TextCatalog.IsVisibleInDemo(t.FileName, WordSorter.MaxTraceWords))
                .ToList());
        });

        // Превью текста: первые строки + сколько в нём слов.
        api.MapPost("/texts/preview", (TextPreviewRequest req, ITextStore store) =>
        {
            try
            {
                string text = store.ReadText(req.FileName);
                return Results.Ok(WordDtoMapper.ToPreview(req.FileName, text));
            }
            catch (Exception ex) when (ex is FileNotFoundException or TableFormatException)
            {
                return ApiError(ex);
            }
        });

        // Список алгоритмов сортировки слов.
        api.MapGet("/word-sort/methods", (WordSorter sorter) => Results.Ok(sorter.ListMethods()));

        // Сортировка готового текста: трассировка шагов + подсчёт + файл результата.
        api.MapPost("/word-sort/run", (WordSortRequest req, WordSorter sorter) =>
        {
            try
            {
                var run = sorter.Run(req.FileName, req.Method, req.Ascending);
                return Results.Ok(WordDtoMapper.ToResponse(run));
            }
            catch (Exception ex) when (ex is FileNotFoundException or TableFormatException)
            {
                return ApiError(ex);
            }
        });

        // Случайный текст: генерируем здесь же и сразу сортируем. Без seed — каждый раз новый текст;
        // с seed — тот же текст (пересортировка другим алгоритмом/в другом порядке).
        api.MapPost("/word-sort/run-random", (RandomSortRequest req, WordSorter sorter) =>
        {
            try
            {
                var run = sorter.RunRandom(req.WordCount, req.Seed, req.Method, req.Ascending);
                return Results.Ok(WordDtoMapper.ToResponse(run));
            }
            catch (Exception ex) when (ex is FileNotFoundException or TableFormatException)
            {
                return ApiError(ex);
            }
        });

        // Замер времени одного выбранного алгоритма на тексте заданного объёма.
        // Замер не зависит от настроек демонстрации — всё указывается в запросе.
        api.MapPost("/word-sort/bench", (WordBenchRequest req, WordSorter sorter) =>
        {
            try
            {
                var result = sorter.Benchmark(req.Algorithm, req.Runs, req.WordCount);
                return Results.Ok(WordDtoMapper.ToDto(result));
            }
            catch (Exception ex) when (ex is FileNotFoundException or TableFormatException)
            {
                return ApiError(ex);
            }
        });

        // Скачивание файла результата сортировки слов (лежит в общей папке Data/results).
        api.MapGet("/word-sort/results/{fileName}", (string fileName, ITextStore store) =>
        {
            try
            {
                string path = store.ResultPath(fileName);
                return Results.File(path, "text/csv; charset=utf-8", Path.GetFileName(path));
            }
            catch (FileNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });
    }

    private static IResult ApiError(Exception ex) =>
        ex is FileNotFoundException
            ? Results.NotFound(new { error = ex.Message })
            : Results.BadRequest(new { error = ex.Message });
}
