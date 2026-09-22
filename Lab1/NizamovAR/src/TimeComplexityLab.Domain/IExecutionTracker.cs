namespace TimeComplexityLab.Domain;

public interface IExecutionTracker
{
    long Steps { get; }
    void CountStep();
    void Reset();
}