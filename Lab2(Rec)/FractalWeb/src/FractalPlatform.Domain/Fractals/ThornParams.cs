using System.Text.Json;

namespace FractalPlatform.Domain.Fractals;

public record ThornParams(
    int Width = 800,
    int Height = 800,
    int MaxIter = 500,
    double Bailout = 10000.0,
    double Cx = 9.984,
    double Cy = 7.55,
    double XMin = -2.5,
    double XMax = 2.5,
    double YMin = -2.5,
    double YMax = 2.5)
{
    public static ThornParams FromJson(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object)
            return new ThornParams();

        int GetInt(string name, int def)
        {
            if (json.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var v))
                return v;
            return def;
        }

        double GetDouble(string name, double def)
        {
            if (json.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var v))
                return v;
            return def;
        }

        var width = Math.Clamp(GetInt("width", 800), 100, 4000);
        var height = Math.Clamp(GetInt("height", 800), 100, 4000);
        var maxIter = Math.Clamp(GetInt("maxIter", 500), 10, 10000);
        var bailout = Math.Clamp(GetDouble("bailout", 10000.0), 1.0, 1e12);
        var cx = GetDouble("cx", 9.984);
        var cy = GetDouble("cy", 7.55);
        var xMin = GetDouble("xMin", -2.5);
        var xMax = GetDouble("xMax", 2.5);
        var yMin = GetDouble("yMin", -2.5);
        var yMax = GetDouble("yMax", 2.5);

        return new ThornParams(width, height, maxIter, bailout, cx, cy, xMin, xMax, yMin, yMax);
    }
}