using LabApp.Core;
using Xunit;

public class BenchmarkRunnerTests
{
    [Fact] public void CompareStacks_ProducesPoints()
    {
        var result = BenchmarkRunner.CompareStacks(new[] { 10, 100 }, warmupRuns: 1, iterations: 2);
        Assert.Equal("Stack", result.Structure);
        Assert.Equal(2, result.Points.Count);
        Assert.All(result.Points, p =>
        {
            Assert.True(p.CustomMs >= 0);
            Assert.True(p.StandardMs >= 0);
        });
    }

    [Fact] public void CompareQueues_ProducesPoints()
    {
        var result = BenchmarkRunner.CompareQueues(new[] { 10, 100 }, warmupRuns: 1, iterations: 2);
        Assert.Equal("Queue", result.Structure);
        Assert.Equal(2, result.Points.Count);
    }

    [Fact] public void CompareStacks_SizesArePreserved()
    {
        var sizes = new[] { 10, 50, 100 };
        var result = BenchmarkRunner.CompareStacks(sizes, warmupRuns: 1, iterations: 1);
        Assert.Equal(sizes, result.Points.Select(p => p.Size).ToArray());
    }
}