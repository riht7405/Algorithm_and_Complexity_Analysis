using LabApp.Core;
using Xunit;

public class ScenarioRunnerTests
{
    [Fact] public void RunStack_SimpleTrace()
    {
        var result = ScenarioRunner.RunStack("1,1 1,2 3 2 4", () => new LinkedListStack<ScalarValue>());
        Assert.Equal(5, result.Trace.Count);
        Assert.Contains("top -> 2", result.Trace[2].Result);
        Assert.Contains("pop -> 2", result.Trace[3].Result);
        Assert.Equal("false", result.Trace[4].Result);
        Assert.True(result.Timing.ExecuteMs >= 0);
    }

    [Fact] public void RunQueue_FIFO()
    {
        var result = ScenarioRunner.RunQueue("1,1 1,2 2", () => new ListQueue<ScalarValue>());
        Assert.Equal(3, result.Trace.Count);
        Assert.Contains("1", result.Trace[2].Result);
    }

    [Fact] public void RunStack_ErrorOnEmpty_IsRecordedNotThrown()
    {
        var result = ScenarioRunner.RunStack("2", () => new LinkedListStack<ScalarValue>());
        Assert.Single(result.Trace);
        Assert.Contains("Ошибка", result.Trace[0].Result);
    }

    [Fact] public void BothStacks_SameTrace()
    {
        var a = ScenarioRunner.RunStack("1,1 1,2 3 5 4", () => new LinkedListStack<ScalarValue>());
        var b = ScenarioRunner.RunStack("1,1 1,2 3 5 4", () => new StandardStackAdapter<ScalarValue>());
        Assert.Equal(a.Trace.Count, b.Trace.Count);
        for (int i = 0; i < a.Trace.Count; i++)
            Assert.Equal(a.Trace[i].Result, b.Trace[i].Result);
    }
}