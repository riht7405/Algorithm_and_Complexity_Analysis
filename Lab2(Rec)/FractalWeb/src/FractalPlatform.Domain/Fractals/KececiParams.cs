using System.Text.Json;

namespace FractalPlatform.Domain.Fractals;

public record KececiParams(
    int NumChildren = 5,
    int MaxLevel = 4,
    string GrowthMode = "Outward",
    double Radius = 200.0,
    double Shrink = 0.5)
{
    public static KececiParams FromJson(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object)
            return new KececiParams();

        int GetInt(string name, int def) => json.TryGetProperty(name, out var p) && p.TryGetInt32(out var v) ? v : def;
        double GetDouble(string name, double def) => json.TryGetProperty(name, out var p) && p.TryGetDouble(out var v) ? v : def;
        string GetString(string name, string def) => json.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? def : def;

        return new KececiParams(
            NumChildren: Math.Clamp(GetInt("numChildren", 5), 2, 12),
            MaxLevel: Math.Clamp(GetInt("maxLevel", 4), 1, 7),
            GrowthMode: GetString("growthMode", "Outward"),
            Radius: Math.Clamp(GetDouble("radius", 200.0), 10, 1000),
            Shrink: Math.Clamp(GetDouble("shrink", 0.5), 0.1, 0.9)
        );
    }
}