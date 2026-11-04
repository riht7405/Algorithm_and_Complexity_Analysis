using LabApp.Core;

namespace LabApp.Api;

public static class Endpoints
{
    private const int MaxInputBytes = 100_000;
    private const int MaxBenchmarkSize = 1_000_000;

    public static void MapLabEndpoints(this WebApplication app)
    {
        app.MapPost("/api/runs/stack", RunStack);
        app.MapPost("/api/runs/queue", RunQueue);
        app.MapPost("/api/expressions/postfix/evaluate", EvaluatePostfix);
        app.MapPost("/api/expressions/infix/to-postfix", InfixToPostfix);
        app.MapPost("/api/lists/operations/{op}", ListOperation);
        app.MapPost("/api/trees/parse-prefix", ParsePrefix);
        app.MapGet("/api/benchmarks/stacks", BenchStacks);
        app.MapGet("/api/benchmarks/queues", BenchQueues);
    }

    private static IResult RunStack(RunRequest req)
    {
        if (req is null || req.Input is null) return Results.BadRequest(new { error = "input required" });
        if (req.Input.Length > MaxInputBytes) return Results.BadRequest(new { error = "input too large" });

        var result = ScenarioRunner.RunStack(req.Input, () => new LinkedListStack<ScalarValue>());
        return Results.Ok(ToRunResponse(result));
    }

    private static IResult RunQueue(RunRequest req)
    {
        if (req is null || req.Input is null) return Results.BadRequest(new { error = "input required" });
        if (req.Input.Length > MaxInputBytes) return Results.BadRequest(new { error = "input too large" });

        var result = ScenarioRunner.RunQueue(req.Input, () => new ListQueue<ScalarValue>());
        return Results.Ok(ToRunResponse(result));
    }

    private static RunResponse ToRunResponse(ScenarioResult r) =>
        new(
            r.Trace.Select(t => new TraceDto(t.Index, t.Operation, t.Argument, t.Result)).ToList(),
            r.FinalState,
            new TimingDto(r.Timing.ReadMs, r.Timing.ParseMs, r.Timing.ExecuteMs, r.Timing.TotalMs));

    private static IResult EvaluatePostfix(ExpressionRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Expression))
            return Results.BadRequest(new { error = "expression required" });
        if (req.Expression.Length > MaxInputBytes)
            return Results.BadRequest(new { error = "expression too large" });

        var tokens = Tokenizer.Tokenize(req.Expression);
        if (!tokens.IsSuccess) return Results.Ok(new EvalResponse(false, null, tokens.Error));

        var value = PostfixEvaluator.Evaluate(tokens.Value);
        return Results.Ok(value.IsSuccess
            ? new EvalResponse(true, value.Value, null)
            : new EvalResponse(false, null, value.Error));
    }

    private static IResult InfixToPostfix(ExpressionRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Expression))
            return Results.BadRequest(new { error = "expression required" });
        if (req.Expression.Length > MaxInputBytes)
            return Results.BadRequest(new { error = "expression too large" });

        var tokens = Tokenizer.Tokenize(req.Expression);
        if (!tokens.IsSuccess) return Results.Ok(new PostfixResponse(false, null, tokens.Error));

        var postfix = ShuntingYard.ToPostfix(tokens.Value);
        if (!postfix.IsSuccess) return Results.Ok(new PostfixResponse(false, null, postfix.Error));

        return Results.Ok(new PostfixResponse(true, string.Join(" ", postfix.Value.Select(t => t.Lexeme)), null));
    }

    private static IResult ListOperation(string op, ListRequest req)
    {
        if (req?.Items is null) return Results.BadRequest(new { error = "items required" });
        if (req.Items.Length > MaxInputBytes) return Results.BadRequest(new { error = "too many items" });

        var list = new SinglyLinkedList<int>();
        foreach (var v in req.Items) list.AddLast(v);

        var before = req.Items.ToArray();

        try
        {
            switch (op)
            {
                case "reverse":
                    LinkedListAlgorithms.Reverse(list);
                    return Ok(before, list.Items.ToArray(), null);

                case "moveLastToFront":
                    LinkedListAlgorithms.MoveLastToFront(list);
                    return Ok(before, list.Items.ToArray(), null);

                case "moveFirstToEnd":
                    LinkedListAlgorithms.MoveFirstToEnd(list);
                    return Ok(before, list.Items.ToArray(), null);

                case "countDistinct":
                    return Ok(before, list.Items.ToArray(), new { count = LinkedListAlgorithms.CountDistinct(list) });

                case "removeNonUnique":
                    LinkedListAlgorithms.RemoveNonUnique(list);
                    return Ok(before, list.Items.ToArray(), null);

                case "insertSelfAfterX":
                    if (req.Arg is null) return Results.BadRequest(new { error = "arg (x) required" });
                    LinkedListAlgorithms.InsertSelfAfterX(list, req.Arg.Value);
                    return Ok(before, list.Items.ToArray(), null);

                case "sortedInsert":
                    if (req.Arg is null) return Results.BadRequest(new { error = "arg (e) required" });
                    LinkedListAlgorithms.SortedInsert(list, req.Arg.Value);
                    return Ok(before, list.Items.ToArray(), null);

                case "removeAll":
                    if (req.Arg is null) return Results.BadRequest(new { error = "arg (e) required" });
                    LinkedListAlgorithms.RemoveAll(list, req.Arg.Value);
                    return Ok(before, list.Items.ToArray(), null);

                case "insertBefore":
                    if (req.Arg is null || req.Arg2 is null)
                        return Results.BadRequest(new { error = "arg (e) and arg2 (f) required" });
                    LinkedListAlgorithms.InsertBefore(list, req.Arg.Value, req.Arg2.Value);
                    return Ok(before, list.Items.ToArray(), null);

                case "append":
                    if (req.SecondList is null) return Results.BadRequest(new { error = "secondList required" });
                    var second = new SinglyLinkedList<int>();
                    foreach (var v in req.SecondList) second.AddLast(v);
                    LinkedListAlgorithms.Append(list, second);
                    return Ok(before, list.Items.ToArray(), null);

                case "splitByValue":
                    if (req.Arg is null) return Results.BadRequest(new { error = "arg (x) required" });
                    var (left, right) = LinkedListAlgorithms.SplitByValue(list, req.Arg.Value);
                    return Ok(before, list.Items.ToArray(), new
                    {
                        left = left.Items.ToArray(),
                        right = right.Items.ToArray()
                    });

                case "double":
                    LinkedListAlgorithms.Double(list);
                    return Ok(before, list.Items.ToArray(), null);

                case "swap":
                    if (req.Arg is null || req.Arg2 is null)
                        return Results.BadRequest(new { error = "arg (a) and arg2 (b) required" });
                    var swapped = LinkedListAlgorithms.Swap(list, req.Arg.Value, req.Arg2.Value);
                    return Ok(before, list.Items.ToArray(), new { swapped });

                default:
                    return Results.BadRequest(new { error = $"unknown operation: {op}" });
            }
        }
        catch (Exception ex)
        {
            return Results.Ok(new ListResponse(false, ex.Message, before, null, null));
        }
    }

    private static IResult Ok(int[] before, int[] after, object? result) =>
        Results.Ok(new ListResponse(true, null, before, after, result));

    private static IResult ParsePrefix(TreeRequest req)
    {
        if (req is null || string.IsNullOrEmpty(req.Expression))
            return Results.BadRequest(new { error = "expression required" });
        if (req.Expression.Length > MaxInputBytes)
            return Results.BadRequest(new { error = "expression too large" });

        var tree = PrefixParser.ParsePrefix(req.Expression);
        if (tree.Root is null)
        {
            return Results.Ok(new TreeResponse(
                true, null, null,
                Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), 0));
        }

        var pre = TreeTraversal.PreOrder(tree.Root).Select(c => (int)c).ToArray();
        var ino = TreeTraversal.InOrder(tree.Root).Select(c => (int)c).ToArray();
        var post = TreeTraversal.PostOrder(tree.Root).Select(c => (int)c).ToArray();
        var height = TreeTraversal.Height(tree.Root);
        var root = ToNodeDto(tree.Root);

        return Results.Ok(new TreeResponse(true, null, root, pre, ino, post, height));
    }

    private static TreeNodeDto? ToNodeDto<T>(TreeNode<T>? node)
    {
        if (node is null) return null;
        return new TreeNodeDto(
            node.Value?.ToString() ?? "",
            ToNodeDto(node.Left),
            ToNodeDto(node.Right));
    }

    private static IResult BenchStacks(string? sizes)
    {
        var parsed = ParseSizes(sizes);
        if (parsed is null) return Results.BadRequest(new { error = "invalid sizes" });

        var result = BenchmarkRunner.CompareStacks(parsed, warmupRuns: 2, iterations: 5);
        return Results.Ok(ToBenchResponse(result));
    }

    private static IResult BenchQueues(string? sizes)
    {
        var parsed = ParseSizes(sizes);
        if (parsed is null) return Results.BadRequest(new { error = "invalid sizes" });

        var result = BenchmarkRunner.CompareQueues(parsed, warmupRuns: 2, iterations: 5);
        return Results.Ok(ToBenchResponse(result));
    }

    private static int[]? ParseSizes(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return BenchmarkRunner.DefaultSizes;
        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var result = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out var v) || v <= 0 || v > MaxBenchmarkSize) return null;
            result[i] = v;
        }
        return result;
    }

    private static BenchmarkResponse ToBenchResponse(BenchmarkResult r) =>
        new(r.Structure, r.Operation, r.Points
            .Select(p => new BenchmarkPointDto(p.Size, p.CustomMs, p.StandardMs, p.Ratio))
            .ToList());
}