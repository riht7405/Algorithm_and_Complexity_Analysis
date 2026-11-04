using System.Diagnostics;

namespace LabApp.Core;

public sealed record BenchmarkPoint(int Size, double CustomMs, double StandardMs)
{
    public double Ratio => StandardMs > 0 ? CustomMs / StandardMs : 0;
}

public sealed record BenchmarkResult(
    string Structure,
    string Operation,
    IReadOnlyList<BenchmarkPoint> Points);

public static class BenchmarkRunner
{
    public static BenchmarkResult CompareStacks(
        int[] sizes,
        int warmupRuns = 5, int iterations = 20,
        int seed = 42)
    {
        var points = new List<BenchmarkPoint>(sizes.Length);
        foreach (var n in sizes)
        {
            var values = MakeValues(n, seed);
            double custom = Measure(() =>
            {
                var s = new LinkedListStack<ScalarValue>();
                foreach (var v in values) s.Push(v);
                while (!s.IsEmpty()) s.Pop();
            }, warmupRuns, iterations);

            double standard = Measure(() =>
            {
                var s = new StandardStackAdapter<ScalarValue>();
                foreach (var v in values) s.Push(v);
                while (!s.IsEmpty()) s.Pop();
            }, warmupRuns, iterations);

            points.Add(new BenchmarkPoint(n, custom, standard));
        }
        return new BenchmarkResult("Stack", "Push+Pop", points);
    }

    public static BenchmarkResult CompareQueues(
        int[] sizes,
        int warmupRuns = 5, int iterations = 20,
        int seed = 42)
    {
        var points = new List<BenchmarkPoint>(sizes.Length);
        foreach (var n in sizes)
        {
            var values = MakeValues(n, seed);
            double custom = Measure(() =>
            {
                var q = new ListQueue<ScalarValue>();
                foreach (var v in values) q.Enqueue(v);
                while (!q.IsEmpty()) q.Dequeue();
            }, warmupRuns, iterations);

            double standard = Measure(() =>
            {
                var q = new StandardQueueAdapter<ScalarValue>();
                foreach (var v in values) q.Enqueue(v);
                while (!q.IsEmpty()) q.Dequeue();
            }, warmupRuns, iterations);

            points.Add(new BenchmarkPoint(n, custom, standard));
        }
        return new BenchmarkResult("Queue", "Enqueue+Dequeue", points);
    }

    public static int[] DefaultSizes => new[] { 100, 1_000, 10_000, 100_000 };

    private static ScalarValue[] MakeValues(int n, int seed)
    {
        var rng = new Random(seed);
        var values = new ScalarValue[n];
        for (int i = 0; i < n; i++)
            values[i] = new ScalarValue(rng.Next(0, 1_000_000).ToString(), true);
        return values;
    }

    private static double Measure(Action run, int warmupRuns, int iterations)
    {
        for (int w = 0; w < warmupRuns; w++) run();

        var times = new double[iterations];
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            run();
            sw.Stop();
            times[i] = sw.Elapsed.TotalMilliseconds;
        }
        Array.Sort(times);
        return times[times.Length / 2];
    }
}