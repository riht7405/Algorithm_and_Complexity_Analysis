using System.Text.Json;

namespace FractalPlatform.Domain.Abstractions;

public record FractalRequest
{
    public string FractalId { get; init; } = string.Empty;
    public JsonElement Parameters { get; init; }
    public RenderQuality Quality { get; init; } = RenderQuality.Standard;
}

public enum RenderQuality
{
    Preview,    // низкое разрешение, быстрый ответ
    Standard,   // обычный рендер
    High        // полное разрешение, для экспорта
}