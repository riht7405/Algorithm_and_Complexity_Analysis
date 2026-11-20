using LabApp.Core;
using LabApp.Core.ExternalSort;
using Microsoft.AspNetCore.Mvc;

namespace LabApp.Api;

/// <summary>
/// Все API-маршруты приложения: список таблиц, превью, методы сортировки,
/// запуск сортировки и результаты. Новый маршрут добавляется в этот класс.
/// </summary>
public static class SortEndpoints
{
    public static void MapSortEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        // Список таблиц в папке Data.
        api.MapGet("/tables", (IDataStore store) => Results.Ok(store.ListTables()));

        // Список методов внешней сортировки.
        api.MapGet("/methods", (ExternalSorter sorter) => Results.Ok(sorter.ListMethods()));

        // Превью таблицы (исходная таблица для визуализации).
        api.MapPost("/tables/preview", (PreviewRequest req, IDataStore store) =>
        {
            try
            {
                var table = store.ReadTable(req.FileName, req.HasHeader);
                return Results.Ok(DtoMapper.ToPreview(table));
            }
            catch (Exception ex) when (ex is FileNotFoundException or TableFormatException)
            {
                return ApiError(ex);
            }
        });

        // Запуск внешней сортировки: трассировка шагов + результирующий файл.
        api.MapPost("/external-sort/run", (SortRequest req, IDataStore store, ExternalSorter sorter) =>
        {
            try
            {
                var table = store.ReadTable(req.FileName, req.HasHeader);
                var run = sorter.Run(table, req.Method, req.KeyColumn, req.Ascending);
                return Results.Ok(DtoMapper.ToResponse(run));
            }
            catch (Exception ex) when (ex is FileNotFoundException or TableFormatException)
            {
                return ApiError(ex);
            }
        });

        // Список результирующих файлов.
        api.MapGet("/results", (IDataStore store) => Results.Ok(store.ListResults()));

        // Превью результирующего файла (показ «что записалось в файл»).
        api.MapGet("/results/{fileName}/preview", (string fileName, bool hasHeader, IDataStore store) =>
        {
            try
            {
                var table = store.ReadResult(fileName, hasHeader);
                return Results.Ok(DtoMapper.ToPreview(table));
            }
            catch (Exception ex) when (ex is FileNotFoundException or TableFormatException)
            {
                return ApiError(ex);
            }
        });

        // Скачивание результирующего файла.
        api.MapGet("/results/{fileName}", (string fileName, IDataStore store) =>
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
