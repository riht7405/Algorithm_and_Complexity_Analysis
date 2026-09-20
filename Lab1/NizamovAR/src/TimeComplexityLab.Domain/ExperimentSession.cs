using System.Diagnostics.Metrics;

namespace TimeComplexityLab.Domain;

public class ExperimentSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AlgorithmId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int NMax { get; set; }
    public int Step { get; set; }
    public int RunsPerPoint { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    public List<Measurement> Measurements { get; set; } = new();
}