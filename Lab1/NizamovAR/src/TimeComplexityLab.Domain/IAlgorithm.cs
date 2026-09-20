namespace TimeComplexityLab.Domain;

public interface IAlgorithm
{
    string Id { get; }
    string DisplayName { get; }
    string Category { get; }
    string TheoreticalComplexity { get; }

    /// <summary>
    /// Генерация входных данных. Вызывается ВНЕ таймера.
    /// </summary>
    object GenerateInput(int n, Random random);

    /// <summary>
    /// Чистая работа алгоритма. Всё, что здесь — замеряется.
    /// Никаких аллокаций, Console.WriteLine, обращений к БД.
    /// </summary>
    void Execute(object input);
}