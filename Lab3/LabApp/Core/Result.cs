namespace LabApp.Core;

public sealed class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    private Result(bool ok, string? error) { IsSuccess = ok; Error = error; }
    public static Result Ok() => new(true, null);
    public static Result Fail(string error) => new(false, error);
}

public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T Value { get; }
    public string? Error { get; }
    private Result(bool ok, T value, string? error) { IsSuccess = ok; Value = value; Error = error; }
    public static Result<T> Ok(T value) => new(true, value, null);
    public static Result<T> Fail(string error) => new(false, default!, error);
}