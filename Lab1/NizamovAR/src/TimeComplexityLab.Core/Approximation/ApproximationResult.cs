namespace TimeComplexityLab.Core.Approximation;

public sealed record ApproximationResult(
    ComplexityFunction Function,
    double C,
    double MSE,
    int PointsCount
);