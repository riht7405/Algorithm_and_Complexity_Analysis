using FractalPlatform.Application.Caching;
using FractalPlatform.Application.Registry;
using FractalPlatform.Domain.Abstractions;
using Microsoft.Extensions.Logging;

namespace FractalPlatform.Application.Services;

public class FractalService
{
    private readonly IFractalRegistry _registry;
    private readonly IFractalCache _cache;
    private readonly ILogger<FractalService> _logger;

    public FractalService(
        IFractalRegistry registry,
        IFractalCache cache,
        ILogger<FractalService> logger)
    {
        _registry = registry;
        _cache = cache;
        _logger = logger;
    }

    public async Task<FractalResult> ComputeAsync(
        string fractalId,
        FractalRequest request,
        CancellationToken ct = default)
    {
        var fractal = _registry.Get(fractalId)
            ?? throw new KeyNotFoundException($"Fractal '{fractalId}' not found.");

        var cacheKey = CacheKey.From(request);

        if (_cache.TryGet(cacheKey, out var cached) && cached is not null)
        {
            _logger.LogDebug("Cache HIT for {FractalId} (key {Key})", fractalId, cacheKey[..8]);
            return cached;
        }

        _logger.LogDebug("Cache MISS for {FractalId}, computing...", fractalId);
        var result = await fractal.ComputeAsync(request, ct);
        result = result with { FractalId = fractalId };

        var ttl = fractal.Metadata.CacheTtl ?? TimeSpan.FromMinutes(5);
        _cache.Set(cacheKey, result, ttl);

        return result;
    }
}