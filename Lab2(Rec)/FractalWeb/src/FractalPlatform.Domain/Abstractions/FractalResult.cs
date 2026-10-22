namespace FractalPlatform.Domain.Abstractions;

public abstract record FractalResult
{
    public string FractalId { get; init; } = string.Empty;
    public TimeSpan ComputeTime { get; init; }
}

// Результат для Тёрна (и других escape-time фракталов)
public record IterationMapResult : FractalResult
{
    public int Width { get; init; }
    public int Height { get; init; }
    public int MaxIterations { get; init; }
    public ushort[] Iterations { get; init; } = []; // ushort для экономии памяти
    public double XMin, XMax, YMin, YMax;
}

public record CellGridResult : FractalResult
{
    public int Rows { get; init; }
    public int Cols { get; init; }
    public int States { get; init; }
    public byte[] Cells { get; init; } = [];
}

public record Point2D(float X, float Y);

public record PointPathResult : FractalResult
{
    public Point2D[] Points { get; init; } = [];
    public int MaxLevel { get; init; }
}

public record SpectrumResult : FractalResult
{
    public int Width { get; init; }
    public int Height { get; init; }
    public float[] Density { get; init; } = []; // Нормализованная плотность от 0.0 до 1.0
}