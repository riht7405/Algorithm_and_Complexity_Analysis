using System.Globalization;
using System.Text.Json;
using FractalPlatform.Application.Caching;
using FractalPlatform.Application.Registry;
using FractalPlatform.Application.Services;
using FractalPlatform.Domain.Abstractions;
using FractalPlatform.Domain.Fractals;
using FractalPlatform.Infrastructure.Binary;
using FractalPlatform.Infrastructure.Compression;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache(o => o.SizeLimit = 512L * 1024 * 1024);
builder.Services.AddSingleton<IFractalCache, MemoryFractalCache>();

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
     .WithExposedHeaders(
         "X-Fractal-Width", "X-Fractal-Height", "X-Fractal-MaxIter",
         "X-Fractal-Bounds", "X-Fractal-ComputeMs",
         "X-Fractal-ResultType", "X-Fractal-Encoding",
         "X-Fractal-UncompressedSize", "X-Fractal-Rows",
         "X-Fractal-Cols", "X-Fractal-States", "X-Fractal-MaxLevel")));

// --- Регистрация всех фракталов ---
builder.Services.AddSingleton<IFractal, ThornFractal>();
builder.Services.AddSingleton<IFractal, KuvinaFractal>();
builder.Services.AddSingleton<IFractal, KececiFractal>();
builder.Services.AddSingleton<IFractal, HofstadterFractal>();

builder.Services.AddSingleton<IFractalRegistry, FractalRegistry>();
builder.Services.AddScoped<FractalService>();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (app.Environment.IsDevelopment())
        {
            ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        }
    }
});
app.UseCors();

var schemas = new Dictionary<string, object[]>
{
    ["thorn"] = new object[]
    {
        new { name = "cx", label = "Cx", type = "number", step = 0.001, defaultValue = 9.984 },
        new { name = "cy", label = "Cy", type = "number", step = 0.001, defaultValue = 7.55 },
        new { name = "maxIter", label = "MaxIter", type = "int", min = 10, max = 5000, defaultValue = 500 },
        new { name = "width", label = "Width", type = "int", min = 100, max = 2000, defaultValue = 800 },
        new { name = "height", label = "Height", type = "int", min = 100, max = 2000, defaultValue = 800 }
    },
    ["kuvina"] = new object[]
    {
        new { name = "generations", label = "Generations", type = "int", min = 10, max = 1024, defaultValue = 256 },
        new { name = "modulus", label = "Modulus", type = "int", min = 2, max = 16, defaultValue = 4 },
        new { name = "variation", label = "Variation", type = "select", options = new[] { "Normal", "Bi", "Shadow", "BiSkew", "ShadowSkew" }, defaultValue = "Normal" }
    },
    ["kececi"] = new object[]
    {
        new { name = "numChildren", label = "Children", type = "int", min = 2, max = 12, defaultValue = 5 },
        new { name = "maxLevel", label = "Max Level", type = "int", min = 1, max = 7, defaultValue = 4 },
        new { name = "growthMode", label = "Growth Mode", type = "select", options = new[] { "Outward", "Inward", "Tangent" }, defaultValue = "Outward" },
        new { name = "shrink", label = "Shrink", type = "number", step = 0.05, min = 0.1, max = 0.9, defaultValue = 0.5 }
    },
    ["hofstadter"] = new object[]
    {
        new { name = "maxQ", label = "Max Denominator (q)", type = "int", min = 5, max = 100, defaultValue = 30 },
        new { name = "width", label = "Width", type = "int", min = 200, max = 1600, defaultValue = 800 },
        new { name = "height", label = "Height", type = "int", min = 200, max = 1200, defaultValue = 600 }
    }
};

app.MapGet("/api/fractals", (IFractalRegistry registry) =>
    registry.All.Select(f => new
    {
        f.Id,
        f.Metadata.DisplayName,
        Kind = f.Metadata.Kind.ToString(),
        f.Metadata.ResultType,
        f.Metadata.IsHeavy,
        Parameters = schemas.GetValueOrDefault(f.Id, Array.Empty<object>())
    }));

app.MapPost("/api/fractals/{id}/compute", async (
    string id,
    HttpRequest request,
    HttpResponse response,
    FractalService service,
    CancellationToken ct) =>
{
    using var doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct);
    var fractalRequest = new FractalRequest { FractalId = id, Parameters = doc.RootElement.Clone() };

    FractalResult result;
    try
    {
        result = await service.ComputeAsync(id, fractalRequest, ct);
    }
    catch (KeyNotFoundException ex)
    {
        response.StatusCode = 404;
        await response.WriteAsJsonAsync(new { error = ex.Message }, ct);
        return;
    }

    // --- ОСОБЫЙ СЛУЧАЙ: Векторный путь (Кечеджи) ---
    if (result is PointPathResult path)
    {
        response.StatusCode = 200;
        response.ContentType = "application/json";
        response.Headers["X-Fractal-ResultType"] = "PointPath";
        response.Headers["X-Fractal-ComputeMs"] = result.ComputeTime.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture);
        response.Headers["X-Fractal-MaxLevel"] = path.MaxLevel.ToString(CultureInfo.InvariantCulture);

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // ВАЖНО: чтобы x/y были строчными
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
            };
            await JsonSerializer.SerializeAsync(response.Body, path.Points, options, ct);
        }
        catch (OperationCanceledException)
        {
            // Игнорируем отмену запроса клиентом
        }
        return;
    }

    // --- БИНАРНЫЕ ДАННЫЕ (Тёрн, Кувина, Хофштадтер) ---
    byte[] rawBytes;
    string resultType;

    if (result is IterationMapResult im)
    {
        rawBytes = BinaryPacker.PackUInt16(im.Iterations);
        resultType = "IterationMap";
        response.Headers["X-Fractal-Width"] = im.Width.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-Fractal-Height"] = im.Height.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-Fractal-MaxIter"] = im.MaxIterations.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-Fractal-Bounds"] = string.Format(CultureInfo.InvariantCulture,
            "{0},{1},{2},{3}", im.XMin, im.XMax, im.YMin, im.YMax);
    }
    else if (result is CellGridResult cg)
    {
        rawBytes = cg.Cells;
        resultType = "CellGrid";
        response.Headers["X-Fractal-Rows"] = cg.Rows.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-Fractal-Cols"] = cg.Cols.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-Fractal-States"] = cg.States.ToString(CultureInfo.InvariantCulture);
    }
    else if (result is SpectrumResult sp)
    {
        rawBytes = new byte[sp.Density.Length * 4]; // float = 4 байта
        Buffer.BlockCopy(sp.Density, 0, rawBytes, 0, rawBytes.Length);
        resultType = "Spectrum";
        response.Headers["X-Fractal-Width"] = sp.Width.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-Fractal-Height"] = sp.Height.ToString(CultureInfo.InvariantCulture);
    }
    else
    {
        response.StatusCode = 501;
        await response.WriteAsJsonAsync(new { error = $"Result type '{result.GetType().Name}' not supported." }, ct);
        return;
    }

    var acceptEncoding = request.Headers.AcceptEncoding.ToString();
    var acceptsBrotli = acceptEncoding.Contains("br", StringComparison.OrdinalIgnoreCase);
    var payload = acceptsBrotli ? FractalCompressor.CompressBrotli(rawBytes) : rawBytes;

    response.StatusCode = 200;
    response.ContentType = "application/octet-stream";
    response.ContentLength = payload.Length;

    if (acceptsBrotli) response.Headers.ContentEncoding = "br";

    response.Headers["X-Fractal-ResultType"] = resultType;
    response.Headers["X-Fractal-ComputeMs"] = result.ComputeTime.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture);
    response.Headers["X-Fractal-Encoding"] = acceptsBrotli ? "br" : "identity";
    response.Headers["X-Fractal-UncompressedSize"] = rawBytes.Length.ToString(CultureInfo.InvariantCulture);

    await response.Body.WriteAsync(payload, ct);
});

app.Run();