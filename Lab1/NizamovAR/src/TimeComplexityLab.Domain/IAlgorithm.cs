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
    /// Генерация источника МАКСИМАЛЬНОГО размера для всего эксперимента.
    /// Вызывается один раз на весь RunExperimentAsync.
    /// Для 1D: maxPoint.N — длина вектора.
    /// Для 2D: maxPoint.N и maxPoint.M — размеры источника.
    /// </summary>
    object GenerateSource(ExperimentPoint maxPoint, Random random);

    /// <summary>
    /// Нарезка источника до размеров точки.
    /// Для 1D: берём первые point.N элементов.
    /// Для 2D: берём [0..point.N, 0..point.M].
    /// </summary>
    object Slice(object source, ExperimentPoint point);

    /// <summary>
    /// Копия. Мутирующие алгоритмы (сортировки) обязаны клонировать,
    /// чтобы следующий прогон не получил уже обработанный вход.
    /// Не-мутирующие могут вернуть source as is.
    /// </summary>
    object Clone(object input);

    void Execute(object input, IExecutionTracker tracker);
}