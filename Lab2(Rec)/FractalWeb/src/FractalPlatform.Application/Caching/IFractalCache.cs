using FractalPlatform.Domain.Abstractions;

namespace FractalPlatform.Application.Caching;

public interface IFractalCache
{
    bool TryGet(string key, out FractalResult? result);
    void Set(string key, FractalResult result, TimeSpan ttl);
}