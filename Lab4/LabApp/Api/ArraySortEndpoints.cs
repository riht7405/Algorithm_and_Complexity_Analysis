using LabApp.Api;
using LabApp.Core;
using LabApp.Core.ArraySort;

namespace LabApp.Api;

/// <summary>
/// API-маршруты задания 1: список алгоритмов внутренней сортировки, случайный массив,
/// запуск сортировки с трассировкой и замеры времени. Новый маршрут добавляется в этот класс.
/// </summary>
public static class ArraySortEndpoints
{
    public static void MapArraySortEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        // Список алгоритмов внутренней сортировки (с группами по классификации задания).
        api.MapGet("/array-sort/methods", (ArraySorter sorter) => Results.Ok(sorter.ListMethods()));

        // Случайный массив: без seed — каждый раз новый; с seed — тот же массив
        // (пересортировка другим алгоритмом или в другом порядке).
        api.MapPost("/array-sort/random", (ArrayRandomRequest req, ArraySorter sorter) =>
        {
            try
            {
                (int[] values, int seed) = sorter.RandomArray(req.Count, req.Seed);
                return Results.Ok(new ArrayRandomResponse(values, seed));
            }
            catch (Exception ex) when (ex is TableFormatException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Внутренняя сортировка массива: полная трассировка шагов. Сортируется копия массива.
        api.MapPost("/array-sort/run", (ArraySortRequest req, ArraySorter sorter) =>
        {
            try
            {
                int[] values = ArraySorter.ParseValues(req.Values);
                var run = sorter.Run(values, req.Method, req.Ascending);
                return Results.Ok(ArrayDtoMapper.ToResponse(run));
            }
            catch (Exception ex) when (ex is TableFormatException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Замер времени одного выбранного алгоритма на массиве заданного объёма.
        // Замер не зависит от настроек демонстрации — всё указывается в запросе.
        api.MapPost("/array-sort/bench", (ArrayBenchRequest req, ArraySorter sorter) =>
        {
            try
            {
                var result = sorter.Benchmark(req.Algorithm, req.Runs, req.ElementCount);
                return Results.Ok(ArrayDtoMapper.ToDto(result));
            }
            catch (Exception ex) when (ex is TableFormatException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }
}
