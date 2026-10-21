namespace FractalPlatform.Domain.Abstractions;

public record FractalMetadata(
    string DisplayName,
    FractalKind Kind,
    string ResultType,
    bool IsHeavy = false,
    TimeSpan? CacheTtl = null
);

public enum FractalKind { EscapeTime, Geometric, Spectral, LSystem, Ifs }