using System.Diagnostics;
using System.Numerics;
using FractalPlatform.Domain.Abstractions;

namespace FractalPlatform.Domain.Fractals;

public class KececiFractal : IFractal
{
    public string Id => "kececi";

    public FractalMetadata Metadata { get; } = new(
        "Kececi Curve",
        FractalKind.Geometric,
        "PointPath",
        IsHeavy: false,
        CacheTtl: TimeSpan.FromMinutes(30)
    );

    public Task<FractalResult> ComputeAsync(FractalRequest request, CancellationToken ct = default)
    {
        var p = KececiParams.FromJson(request.Parameters);
        var sw = Stopwatch.StartNew();

        var points = new List<Point2D>();

        GenerateRecursive(Complex.Zero, p.Radius, 0, p, points);

        sw.Stop();

        var result = new PointPathResult
        {
            FractalId = Id,
            ComputeTime = sw.Elapsed,
            Points = points.ToArray(),
            MaxLevel = p.MaxLevel
        };

        return Task.FromResult<FractalResult>(result);
    }

    private void GenerateRecursive(Complex center, double radius, int level, KececiParams p, List<Point2D> points)
    {
        // Защита от NaN и Infinity
        float x = (float)center.Real;
        float y = (float)center.Imaginary;

        if (float.IsNaN(x) || float.IsInfinity(x)) x = 0f;
        if (float.IsNaN(y) || float.IsInfinity(y)) y = 0f;

        points.Add(new Point2D(x, y));

        if (level >= p.MaxLevel) return;

        double childRadius = radius * p.Shrink;
        double angleStep = 2.0 * Math.PI / p.NumChildren;

        for (int i = 0; i < p.NumChildren; i++)
        {
            double angle = i * angleStep;

            Complex offset = p.GrowthMode switch
            {
                "Inward" => Complex.FromPolarCoordinates(-radius, angle),
                "Tangent" => Complex.FromPolarCoordinates(radius, angle + Math.PI / 2),
                _ => Complex.FromPolarCoordinates(radius, angle) // Outward
            };

            Complex childCenter = center + offset;
            GenerateRecursive(childCenter, childRadius, level + 1, p, points);
        }
    }
}