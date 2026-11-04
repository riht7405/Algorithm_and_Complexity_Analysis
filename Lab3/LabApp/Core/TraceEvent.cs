namespace LabApp.Core;

public sealed record TraceEvent(int Index, string Operation, string? Argument, string Result);