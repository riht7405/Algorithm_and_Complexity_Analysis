using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Presentation.Wpf.Services;

public sealed class SessionSnapshot
{
    public ExperimentSession Session { get; init; } = null!;
    public string DisplayName { get; init; } = "";
    public ExperimentDimension Dimension { get; init; }
    public MeasureUnit MeasureUnit { get; init; }
    public bool FromHistory { get; init; }
}

public static class SessionStore
{
    public static SessionSnapshot? Current { get; private set; }
    public static event Action? CurrentChanged;

    public static void SetFromExperiment(ExperimentSession session, IAlgorithm algorithm)
    {
        Current = new SessionSnapshot
        {
            Session = session,
            DisplayName = algorithm.DisplayName,
            Dimension = algorithm.Dimension,
            MeasureUnit = algorithm.MeasureUnit,
            FromHistory = false
        };
        CurrentChanged?.Invoke();
    }

    public static void SetFromHistory(ExperimentSession session)
    {
        Current = new SessionSnapshot
        {
            Session = session,
            DisplayName = session.DisplayName,
            Dimension = session.Dimension,
            MeasureUnit = session.MeasureUnit,
            FromHistory = true
        };
        CurrentChanged?.Invoke();
    }
}