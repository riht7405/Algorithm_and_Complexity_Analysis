namespace TimeComplexityLab.Domain;

/// <summary>
/// Точка эксперимента. Для одномерных алгоритмов M = null,
/// для двумерных (матрицы, DTW, Хиршберг) M — второй размер.
/// </summary>
public readonly record struct ExperimentPoint(int N, int? M = null)
{
    public override string ToString() =>
        M is null ? $"n={N}" : $"n={N}, m={M}";
}