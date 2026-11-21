using LabApp.Api;
using LabApp.Core;
using LabApp.Core.ExternalSort;

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

app.Run();

public partial class Program { }
