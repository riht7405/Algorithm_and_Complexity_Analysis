namespace LabApp.Core;

public enum CommandKind
{
    PushOrEnqueue = 1,
    PopOrDequeue = 2,
    TopOrPeek = 3,
    IsEmpty = 4,
    Print = 5
}

public sealed record Command(CommandKind Kind, ScalarValue? Argument = null);