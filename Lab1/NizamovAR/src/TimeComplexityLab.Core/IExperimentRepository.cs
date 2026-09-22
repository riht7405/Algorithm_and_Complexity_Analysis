using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core;

public interface IExperimentRepository
{
    Task InitializeAsync();
    Task SaveSessionAsync(ExperimentSession session);
    Task<List<Measurement>> GetCachedMeasurementsAsync(string algorithmId, int n, int m, int runsCount);
    Task SaveMeasurementsAsync(IEnumerable<Measurement> measurements);
}