namespace TimeComplexityLab.Domain;

public class Measurement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public int N { get; set; }
    public int M { get; set; }           // 0 для 1D, m для 2D
    public int RunIndex { get; set; }
    public double ElapsedMilliseconds { get; set; }
    public long Steps { get; set; }      // для MeasureUnit.Steps

    public ExperimentSession Session { get; set; } = null!;
}