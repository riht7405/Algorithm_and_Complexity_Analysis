using Microsoft.EntityFrameworkCore;
using TimeComplexityLab.Core;
using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Infrastructure;

public class SqliteExperimentRepository : IExperimentRepository
{
    public async Task InitializeAsync()
    {
        using var db = new AppDbContext();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task SaveSessionAsync(ExperimentSession session)
    {
        using var db = new AppDbContext();
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
    }

    public async Task<List<Measurement>> GetCachedMeasurementsAsync(
        string algorithmId, int n, int runsCount)
    {
        using var db = new AppDbContext();
        return await db.Measurements
            .Where(m => m.Session!.AlgorithmId == algorithmId && m.N == n)
            .OrderBy(m => m.RunIndex)
            .Take(runsCount)
            .ToListAsync();
    }

    public async Task SaveMeasurementsAsync(IEnumerable<Measurement> measurements)
    {
        using var db = new AppDbContext();
        db.Measurements.AddRange(measurements);
        await db.SaveChangesAsync();
    }
}