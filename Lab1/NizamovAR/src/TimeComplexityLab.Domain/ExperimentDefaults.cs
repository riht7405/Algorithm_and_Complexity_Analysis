namespace TimeComplexityLab.Domain;

/// <summary>
/// Параметры эксперимента по умолчанию для конкретного алгоритма.
/// Гипотеза хранится строкой — так же, как Tag в ComboBox XAML,
/// чтобы не тащить Core.Approximation в Domain.
/// </summary>
public sealed record ExperimentDefaults(
    int NMax,
    int NStep,
    int MMax,
    int MStep,
    int RunsPerPoint,
    string Hypothesis
);