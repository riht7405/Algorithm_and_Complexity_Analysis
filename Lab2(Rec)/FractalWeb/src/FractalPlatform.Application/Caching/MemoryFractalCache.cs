using FractalPlatform.Domain.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace FractalPlatform.Application.Caching;

public class MemoryFractalCache : IFractalCache
{
    private readonly IMemoryCache _cache;

    public MemoryFractalCache(IMemoryCache cache) => _cache = cache;

    public bool TryGet(string key, out FractalResult? result) =>
        _cache.TryGetValue(key, out result);

    public void Set(string key, FractalResult result, TimeSpan ttl)
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
            Size = EstimateSize(result)
        };
        _cache.Set(key, result, options);
    }

    private static long EstimateSize(FractalResult result) => result switch
    {
        IterationMapResult im => im.Iterations.LongLength * sizeof(ushort) + 256,
        _ => 4096
    };
}