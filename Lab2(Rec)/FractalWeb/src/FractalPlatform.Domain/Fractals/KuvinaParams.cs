using System.Text.Json;

namespace FractalPlatform.Domain.Fractals;

public record KuvinaParams(
    int Generations = 256,
    int Modulus = 4,
    int CellSize = 2,
    string Variation = "Normal")
{
    public static KuvinaParams FromJson(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object)
            return new KuvinaParams();

        int GetInt(string name, int def)
        {
            if (json.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var v))
                return v;
            return def;
        }

        string GetString(string name, string def)
        {
            if (json.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String)
                return p.GetString() ?? def;
            return def;
        }

        // Валидация: не даем уронить сервер огромными сетками
        var generations = Math.Clamp(GetInt("generations", 256), 10, 1024);
        var modulus = Math.Clamp(GetInt("modulus", 4), 2, 16);
        var cellSize = Math.Clamp(GetInt("cellSize", 2), 1, 8);
        var variation = GetString("variation", "Normal");

        return new KuvinaParams(generations, modulus, cellSize, variation);
    }
}