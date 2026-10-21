using System.Text.Json;
using FractalPlatform.Domain.Fractals;
using Xunit;

namespace FractalPlatform.Tests;

public class ThornParamsTests
{
    private static ThornParams Parse(string json) =>
        ThornParams.FromJson(JsonDocument.Parse(json).RootElement);

    [Fact]
    public void FromJson_Empty_ReturnsDefaults()
    {
        var p = Parse("{}");
        Assert.Equal(800, p.Width);
        Assert.Equal(500, p.MaxIter);
        Assert.Equal(9.984, p.Cx, 3);
        Assert.Equal(7.55, p.Cy, 2);
    }

    [Fact]
    public void FromJson_ValidValues_AreParsed()
    {
        var p = Parse("""{"width":1000,"height":600,"maxIter":250,"cx":1.5,"cy":-2.0}""");
        Assert.Equal(1000, p.Width);
        Assert.Equal(600, p.Height);
        Assert.Equal(250, p.MaxIter);
        Assert.Equal(1.5, p.Cx);
        Assert.Equal(-2.0, p.Cy);
    }

    [Fact]
    public void FromJson_NullValues_UseDefaults()
    {
        var p = Parse("""{"width":null,"maxIter":null,"cx":null}""");
        Assert.Equal(800, p.Width);
        Assert.Equal(500, p.MaxIter);
        Assert.Equal(9.984, p.Cx, 3);
    }

    [Theory]
    [InlineData(50, 100)]         // слишком маленький → 100
    [InlineData(100000, 4000)]    // слишком большой → 4000
    [InlineData(800, 800)]        // нормальный
    public void FromJson_WidthIsClamped(int input, int expected)
    {
        var p = Parse($$"""{"width":{{input}}}""");
        Assert.Equal(expected, p.Width);
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(9999999, 10000)]
    [InlineData(500, 500)]
    public void FromJson_MaxIterIsClamped(int input, int expected)
    {
        var p = Parse($$"""{"maxIter":{{input}}}""");
        Assert.Equal(expected, p.MaxIter);
    }
}