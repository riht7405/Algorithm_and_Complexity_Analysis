namespace LabApp.Api;

public record RunRequest(string Input);
public record TraceDto(int Index, string Operation, string? Argument, string Result);
public record TimingDto(double ReadMs, double ParseMs, double ExecuteMs, double TotalMs);
public record RunResponse(IReadOnlyList<TraceDto> Trace, string FinalState, TimingDto Timing);
public record ExpressionRequest(string Expression);
public record EvalResponse(bool Ok, double? Value, string? Error);
public record PostfixResponse(bool Ok, string? Postfix, string? Error);
public record ListRequest(int[] Items, int? Arg = null, int? Arg2 = null, int[]? SecondList = null);
public record ListResponse(bool Ok, string? Error, int[]? Before, int[]? After, object? Result);
public record TreeRequest(string Expression);
public record TreeNodeDto(string Value, TreeNodeDto? Left, TreeNodeDto? Right);
public record TreeResponse(
    bool Ok,
    string? Error,
    TreeNodeDto? Root,
    int[]? Preorder,
    int[]? Inorder,
    int[]? Postorder,
    int Height);
public record BenchmarkPointDto(int Size, double CustomMs, double StandardMs, double Ratio);
public record BenchmarkResponse(string Structure, string Operation, IReadOnlyList<BenchmarkPointDto> Points);