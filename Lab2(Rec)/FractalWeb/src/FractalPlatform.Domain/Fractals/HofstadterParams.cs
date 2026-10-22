using System.Text.Json;

namespace FractalPlatform.Domain.Fractals;

public record HofstadterParams(
    int MaxQ = 30,
    int Width = 800,
    int Height = 600)
{
    public static HofstadterParams FromJson(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object)
            return new HofstadterParams();

        int GetInt(string name, int def) => json.TryGetProperty(name, out var p) && p.TryGetInt32(out var v) ? v : def;

        return new HofstadterParams(
            MaxQ: Math.Clamp(GetInt("maxQ", 30), 5, 100),
            Width: Math.Clamp(GetInt("width", 800), 200, 1600),
            Height: Math.Clamp(GetInt("height", 600), 200, 1200)
        );
    }
}