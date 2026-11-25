using LabApp.Api;
using LabApp.Core;
using LabApp.Core.ArraySort;
using LabApp.Core.ExternalSort;
using LabApp.Core.Texts;
using LabApp.Core.WordSort;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5000");

// Таблицы лежат в папке Data рядом с проектом.
builder.Services.AddSingleton<IDataStore>(sp =>
    new FileSystemDataStore(Path.Combine(
        sp.GetRequiredService<IWebHostEnvironment>().ContentRootPath, "Data")));

// Стратегии внешней сортировки.
// Чтобы добавить новую: реализуй IExternalSortStrategy и зарегистрируй её здесь.
builder.Services.AddSingleton<IExternalSortStrategy, DirectMergeStrategy>();
builder.Services.AddSingleton<IExternalSortStrategy, NaturalMergeStrategy>();
builder.Services.AddSingleton<IExternalSortStrategy, MultiwayMergeStrategy>();
builder.Services.AddSingleton<ExternalSorter>();

// Тексты для задания 3 лежат в Data/texts, результаты — в общей папке Data/results.
builder.Services.AddSingleton<ITextStore>(sp =>
{
    string dataDir = Path.Combine(
        sp.GetRequiredService<IWebHostEnvironment>().ContentRootPath, "Data");
    return new FileSystemTextStore(
        Path.Combine(dataDir, "texts"), Path.Combine(dataDir, "results"));
});

// Алгоритмы сортировки слов (задание 3).
// Чтобы добавить новый: реализуй IWordSortAlgorithm и зарегистрируй его здесь —
// он сразу появится в интерфейсе, в API и в замерах времени.
builder.Services.AddSingleton<IWordSortAlgorithm, QuickSortImproved>();
builder.Services.AddSingleton<IWordSortAlgorithm, AbcSort>();
builder.Services.AddSingleton<WordSorter>();

// Алгоритмы внутренней сортировки массива (задание 1).
// Чтобы добавить новый: реализуй IArraySortAlgorithm и зарегистрируй его здесь —
// он сразу появится в интерфейсе, в API и в замерах времени.
builder.Services.AddSingleton<IArraySortAlgorithm, SelectSort>();
builder.Services.AddSingleton<IArraySortAlgorithm, InsertSort>();
builder.Services.AddSingleton<IArraySortAlgorithm, MergeSort>();
builder.Services.AddSingleton<ArraySorter>();

var app = builder.Build();

app.UseDefaultFiles();
// no-cache: браузер всегда перепроверяет статику, поэтому после правок
// app.js / styles.css не нужно чистить кэш — обычного обновления страницы достаточно.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
        ctx.Context.Response.Headers.CacheControl = "no-cache, must-revalidate"
});

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", utc = DateTimeOffset.UtcNow }));

app.MapSortEndpoints();
app.MapWordSortEndpoints();
app.MapArraySortEndpoints();

app.Run();

public partial class Program { }
