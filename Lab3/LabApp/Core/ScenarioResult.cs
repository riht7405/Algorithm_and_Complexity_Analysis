namespace LabApp.Core;

public sealed class ScenarioResult
{
    public IReadOnlyList<TraceEvent> Trace { get; init; } = Array.Empty<TraceEvent>();
    public string FinalState { get; init; } = "[]";
    public Timing Timing { get; init; } = new();
}