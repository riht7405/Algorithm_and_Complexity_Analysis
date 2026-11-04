namespace LabApp.Core;

public sealed class Timing
{
    public double ReadMs { get; set; }
    public double ParseMs { get; set; }
    public double ExecuteMs { get; set; }
    public double TotalMs => ReadMs + ParseMs + ExecuteMs;
    public override string ToString() =>
        $"read={ReadMs:F3}ms, parse={ParseMs:F3}ms, execute={ExecuteMs:F3}ms, total={TotalMs:F3}ms";
}