using System.Security.Cryptography;
using System.Text;
using FractalPlatform.Domain.Abstractions;

namespace FractalPlatform.Application.Caching;

public static class CacheKey
{
    public static string From(FractalRequest request)
    {
        var raw = request.Parameters.GetRawText();
        var input = $"{request.FractalId.ToLowerInvariant()}|{request.Quality}|{raw}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash);
    }
}