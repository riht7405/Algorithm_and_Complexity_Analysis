namespace TimeComplexityLab.Domain;

public interface IAlgorithm
{
    string Id { get; }
    string DisplayName { get; }
    string Category { get; }
    string TheoreticalComplexity { get; }

    ExperimentDimension Dimension { get; }
    MeasureUnit MeasureUnit { get; }

    ExperimentDefaults Defaults { get; }

    /// <summary>
    /// Генерация входных данных. Вызывается ВНЕ таймера.
    /// Для 1D — использует point.N. Для 2D — и point.N, и point.M.
    /// </summary>
    object GenerateInput(ExperimentPoint point, Random random);

    /// <summary>
    /// Чистая работа алгоритма. Только это замеряется.
    /// Если MeasureUnit == Steps — алгоритм сам считает шаги
    /// и записывает их через tracker.
    /// </summary>
    void Execute(object input, IExecutionTracker tracker);
}