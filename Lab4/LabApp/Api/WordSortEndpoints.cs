using LabApp.Api;
using LabApp.Core;
using LabApp.Core.Texts;
using LabApp.Core.WordSort;
using Microsoft.AspNetCore.Mvc;

namespace LabApp.Api;

/// <summary>
/// API-маршруты задания 3: тексты и их генерация, алгоритмы сортировки слов,
/// запуск сортировки с подсчётом вхождений и замеры времени.
/// Новый маршрут добавляется в этот класс.
/// </summary>
public static class WordSortEndpoints
{
    public static void MapWordSortEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        // Список текстов (при первом обращении создаём тексты стандартных длин из задания).
        api.MapGet("/texts", (ITextStore store) =>
        {
            store.EnsureDefaults();
            return Results.Ok(store.ListTexts());
        });

        // Сгенерировать текст заданной длины (100, 500, 1000, 2000, 5000 слов или больше).
        api.MapPost("/texts/generate", (GenerateTextRequest req, ITextStore store) =>
        {
            try
            {
                int n = Math.Clamp(req.WordCount, 10, TextGenerator.MaxGenerateWords);
                // Сид фиксирован длиной: тот же объём → тот же текст (замеры воспроизводимы).
                string content = TextGenerator.Generate(n, seed: n * 7919);
                string fileName = store.SaveGenerated(n, content);
                var words = TextNormalizer.ExtractWords(content);
                return Results.Ok(new TextGeneratedDto(fileName, n, words.Distinct().Count()));
            }
            catch (Exception ex) when (ex is FileNotFoundException or TableFormatException)
            {
                return ApiError(ex);
            }
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

        // Запуск сортировки слов: трассировка шагов + подсчёт + файл результата.
        api.MapPost("/word-sort/run", (WordSortRequest req, WordSorter sorter) =>
        {
            try
            {
                var run = sorter.Run(req.FileName, req.Method, req.Ascending, req.LimitWords);
                return Results.Ok(WordDtoMapper.ToResponse(run));
            }
            catch (Exception ex) when (ex is FileNotFoundException or TableFormatException)
            {
                return ApiError(ex);
            }
        });

        // Замер времени одного алгоритма на тексте заданной длины или из файла.
        api.MapPost("/word-sort/bench", (WordBenchRequest req, WordSorter sorter) =>
        {
            try
            {
                var result = sorter.Benchmark(req.Algorithm, req.Runs, req.WordCount, req.FileName);
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
