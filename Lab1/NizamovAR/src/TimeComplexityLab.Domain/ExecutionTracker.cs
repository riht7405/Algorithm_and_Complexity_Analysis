namespace TimeComplexityLab.Domain;

public sealed class ExecutionTracker : IExecutionTracker
{
    public long Steps { get; private set; }
    public void CountStep() => Steps++;
    public void Reset() => Steps = 0;
}