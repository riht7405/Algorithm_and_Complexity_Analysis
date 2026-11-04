using System.Diagnostics;

namespace LabApp.Core;

public interface ICommandTarget
{
    string Insert(ScalarValue v);
    string Remove();
    string Peek();
    string IsEmpty();
    string Print();
}

public sealed class StackTarget : ICommandTarget
{
    private readonly IStack<ScalarValue> _s;
    public StackTarget(IStack<ScalarValue> s) { _s = s; }
    public string Insert(ScalarValue v) { _s.Push(v); return $"push({v.Raw})"; }
    public string Remove() => $"pop -> {_s.Pop().Raw}";
    public string Peek() => $"top -> {_s.Top().Raw}";
    public string IsEmpty() => _s.IsEmpty() ? "true" : "false";
    public string Print() => "[" + string.Join(", ", _s.Print().Select(x => x.Raw)) + "]";
}

public sealed class QueueTarget : ICommandTarget
{
    private readonly IQueue<ScalarValue> _q;
    public QueueTarget(IQueue<ScalarValue> q) { _q = q; }
    public string Insert(ScalarValue v) { _q.Enqueue(v); return $"enqueue({v.Raw})"; }
    public string Remove() => $"dequeue -> {_q.Dequeue().Raw}";
    public string Peek() => $"peek -> {_q.Peek().Raw}";
    public string IsEmpty() => _q.IsEmpty() ? "true" : "false";
    public string Print() => "[" + string.Join(", ", _q.Print().Select(x => x.Raw)) + "]";
}

public static class ScenarioRunner
{
    public static ScenarioResult RunStack(string input, Func<IStack<ScalarValue>> factory)
        => Run(input, () => new StackTarget(factory()));

    public static ScenarioResult RunQueue(string input, Func<IQueue<ScalarValue>> factory)
        => Run(input, () => new QueueTarget(factory()));

    public static ScenarioResult Run(string input, Func<ICommandTarget> targetFactory)
    {
        var timing = new Timing();
        var sw = Stopwatch.StartNew();

        var parseResult = InputParser.Parse(input);
        timing.ParseMs = sw.Elapsed.TotalMilliseconds;

        if (!parseResult.IsSuccess)
        {
            return new ScenarioResult
            {
                Timing = timing,
                Trace = new[] { new TraceEvent(0, "Error", null, parseResult.Error ?? "parse error") },
                FinalState = "[]"
            };
        }

        var target = targetFactory();
        var commands = parseResult.Value;
        var trace = new List<TraceEvent>(commands.Count);

        sw.Restart();
        for (int i = 0; i < commands.Count; i++)
        {
            var cmd = commands[i];
            string result;
            try
            {
                result = cmd.Kind switch
                {
                    CommandKind.PushOrEnqueue => target.Insert(cmd.Argument!),
                    CommandKind.PopOrDequeue  => target.Remove(),
                    CommandKind.TopOrPeek     => target.Peek(),
                    CommandKind.IsEmpty       => target.IsEmpty(),
                    CommandKind.Print         => target.Print(),
                    _                         => "unknown"
                };
            }
            catch (Exception ex)
            {
                result = "Ошибка: " + ex.Message;
            }
            trace.Add(new TraceEvent(i, cmd.Kind.ToString(), cmd.Argument?.Raw, result));
        }
        timing.ExecuteMs = sw.Elapsed.TotalMilliseconds;

        var finalPrint = target.Print();
        return new ScenarioResult { Trace = trace, FinalState = finalPrint, Timing = timing };
    }
}