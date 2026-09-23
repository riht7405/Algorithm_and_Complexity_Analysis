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
        string algorithmId, int n, int m, int runsCount)
    {
        using var db = new AppDbContext();
        return await db.Measurements
            .Where(x => x.Session!.AlgorithmId == algorithmId
                        && x.N == n
                        && x.M == m)
            .OrderBy(x => x.RunIndex)
            .Take(runsCount)
            .ToListAsync();
    }

    public async Task SaveMeasurementsAsync(IEnumerable<Measurement> measurements)
    {
        using var db = new AppDbContext();
        db.Measurements.AddRange(measurements);
        await db.SaveChangesAsync();
    }

    public async Task<List<ExperimentSession>> GetAllSessionsAsync()
    {
        using var db = new AppDbContext();
        return await db.Sessions
            .Where(s => db.Measurements.Any(m => m.SessionId == s.Id))
            .OrderByDescending(s => s.StartedAtUtc)
            .ToListAsync();
    }

    public async Task<List<Measurement>> GetMeasurementsBySessionsAsync(
        IReadOnlyCollection<Guid> sessionIds)
    {
        using var db = new AppDbContext();
        return await db.Measurements
            .Where(m => sessionIds.Contains(m.SessionId))
            .ToListAsync();
    }

    public async Task DeleteSessionAsync(Guid sessionId)
    {
        using var db = new AppDbContext();
        var session = await db.Sessions
            .Include(s => s.Measurements)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null) return;
        db.Sessions.Remove(session);
        await db.SaveChangesAsync();
    }

    public async Task<int> CleanupEmptySessionsAsync()
    {
        using var db = new AppDbContext();
        var emptySessions = await db.Sessions
            .Where(s => !db.Measurements.Any(m => m.SessionId == s.Id))
            .ToListAsync();

        if (emptySessions.Count == 0) return 0;

        db.Sessions.RemoveRange(emptySessions);
        await db.SaveChangesAsync();
        return emptySessions.Count;
    }
}