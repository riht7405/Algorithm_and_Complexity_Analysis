namespace TimeComplexityLab.Domain;

public class Measurement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public int N { get; set; }
    public int RunIndex { get; set; }
    public double ElapsedMilliseconds { get; set; }

    public ExperimentSession Session { get; set; } = null!;
}