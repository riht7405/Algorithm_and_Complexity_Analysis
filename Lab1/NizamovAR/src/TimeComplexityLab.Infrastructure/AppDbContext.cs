using Microsoft.EntityFrameworkCore;
using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Infrastructure;

public class AppDbContext : DbContext
{
    public DbSet<ExperimentSession> Sessions => Set<ExperimentSession>();
    public DbSet<Measurement> Measurements => Set<Measurement>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=lab.db");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Measurement>()
            .HasOne(m => m.Session)
            .WithMany(s => s.Measurements)
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Measurement>()
            .HasIndex(m => new { m.N, m.RunIndex });

        modelBuilder.Entity<ExperimentSession>()
            .HasIndex(s => new { s.AlgorithmId, s.StartedAtUtc });
    }
}