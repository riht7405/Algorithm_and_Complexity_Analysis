namespace TimeComplexityLab.Core.Approximation;

public enum ComplexityFunction
{
    Constant,   // f(n) = 1
    Logarithmic, // f(n) = log(n)
    Linear,      // f(n) = n
    NLogN,       // f(n) = n * log(n)
    Quadratic,   // f(n) = n^2
    Cubic        // f(n) = n^3
}

public static class ComplexityFunctionExtensions
{
    public static double Evaluate(this ComplexityFunction f, int n)
    {
        double x = n;
        return f switch
        {
            ComplexityFunction.Constant => 1.0,
            ComplexityFunction.Logarithmic => Math.Log(Math.Max(x, 1.0)),
            ComplexityFunction.Linear => x,
            ComplexityFunction.NLogN => x * Math.Log(Math.Max(x, 1.0)),
            ComplexityFunction.Quadratic => x * x,
            ComplexityFunction.Cubic => x * x * x,
            _ => throw new ArgumentOutOfRangeException(nameof(f))
        };
    }

    public static string ToDisplayString(this ComplexityFunction f) => f switch
    {
        ComplexityFunction.Constant => "O(1)",
        ComplexityFunction.Logarithmic => "O(log n)",
        ComplexityFunction.Linear => "O(n)",
        ComplexityFunction.NLogN => "O(n log n)",
        ComplexityFunction.Quadratic => "O(n²)",
        ComplexityFunction.Cubic => "O(n³)",
        _ => f.ToString()
    };
}