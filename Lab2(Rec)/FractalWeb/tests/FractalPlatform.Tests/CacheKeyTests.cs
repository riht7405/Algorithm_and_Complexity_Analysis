using System.Text.Json;
using FractalPlatform.Application.Caching;
using FractalPlatform.Domain.Abstractions;
using Xunit;

namespace FractalPlatform.Tests;

public class CacheKeyTests
{
    private static FractalRequest MakeRequest(string fractalId, string json) =>
        new() { FractalId = fractalId, Parameters = JsonDocument.Parse(json).RootElement.Clone() };

    [Fact]
    public void SameInput_SameKey()
    {
        var a = CacheKey.From(MakeRequest("thorn", """{"cx":1,"cy":2}"""));
        var b = CacheKey.From(MakeRequest("thorn", """{"cx":1,"cy":2}"""));
        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentParams_DifferentKey()
    {
        var a = CacheKey.From(MakeRequest("thorn", """{"cx":1}"""));
        var b = CacheKey.From(MakeRequest("thorn", """{"cx":2}"""));
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void DifferentFractal_DifferentKey()
    {
        var a = CacheKey.From(MakeRequest("thorn", """{}"""));
        var b = CacheKey.From(MakeRequest("kuvina", """{}"""));
        Assert.NotEqual(a, b);
    }
}