using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core;

public interface IExperimentRepository
{
    Task InitializeAsync();
    Task SaveSessionAsync(ExperimentSession session);
    Task<List<Measurement>> GetCachedMeasurementsAsync(string algorithmId, int n, int m, int runsCount);
    Task SaveMeasurementsAsync(IEnumerable<Measurement> measurements);

    Task<List<ExperimentSession>> GetAllSessionsAsync();
    Task<List<Measurement>> GetMeasurementsBySessionsAsync(IReadOnlyCollection<Guid> sessionIds);
    Task DeleteSessionAsync(Guid sessionId);

    /// <summary>
    /// Удаляет все сессии без измерений (артефакты прошлых запусков).
    /// Возвращает количество удалённых.
    /// </summary>
    Task<int> CleanupEmptySessionsAsync();
}