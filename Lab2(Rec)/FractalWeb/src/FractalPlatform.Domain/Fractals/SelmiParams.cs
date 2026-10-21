using System.Text.Json;

namespace FractalPlatform.Domain.Fractals;

public record SelmiParams(int Generations = 80)
{
    public static SelmiParams FromJson(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object)
            return new SelmiParams();

        int GetInt(string name, int def)
        {
            if (json.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var v))
                return v;
            return def;
        }

        // Валидация: не даем уронить сервер
        var generations = Math.Clamp(GetInt("generations", 80), 10, 500);
        return new SelmiParams(generations);
    }
}