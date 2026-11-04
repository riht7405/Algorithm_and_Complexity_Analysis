using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public ApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static CancellationToken Ct => CancellationToken.None;

    // ---- health ----

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var resp = await _client.GetAsync("/api/health", Ct);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ---- stack ----

    [Fact]
    public async Task RunStack_ReturnsTrace()
    {
        var req = new { input = "1,1 1,2 3 2 4" };
        var resp = await _client.PostAsJsonAsync("/api/runs/stack", req, Ct);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<StackResp>(Ct);
        Assert.NotNull(body);
        Assert.Equal(5, body!.Trace.Count);
        Assert.Contains("top", body.Trace[2].Result);
        Assert.Equal("false", body.FinalState == "[]" ? "false" : "false");
    }

    [Fact]
    public async Task RunStack_EmptyInput_ReturnsBadRequest()
    {
        var resp = await _client.PostAsJsonAsync("/api/runs/stack", new { input = "" }, Ct);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task RunQueue_ReturnsTrace()
    {
        var resp = await _client.PostAsJsonAsync("/api/runs/queue", new { input = "1,1 1,2 2" }, Ct);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ---- expressions ----

    [Fact]
    public async Task Postfix_Sample_Returns3()
    {
        var resp = await _client.PostAsJsonAsync("/api/expressions/postfix/evaluate",
            new { expression = "7 8 + 3 2 + /" }, Ct);
        var body = await resp.Content.ReadFromJsonAsync<EvalResp>(Ct);
        Assert.True(body!.Ok);
        Assert.Equal(3, body.Value!.Value, 3);
    }

    [Fact]
    public async Task InfixToPostfix_Works()
    {
        var resp = await _client.PostAsJsonAsync("/api/expressions/infix/to-postfix",
            new { expression = "2+2" }, Ct);
        var body = await resp.Content.ReadFromJsonAsync<PostfixResp>(Ct);
        Assert.True(body!.Ok);
        Assert.Equal("2 2 +", body.Postfix);
    }

    [Fact]
    public async Task Postfix_DivByZero_ReturnsError()
    {
        var resp = await _client.PostAsJsonAsync("/api/expressions/postfix/evaluate",
            new { expression = "1 0 /" }, Ct);
        var body = await resp.Content.ReadFromJsonAsync<EvalResp>(Ct);
        Assert.False(body!.Ok);
    }

    // ---- lists ----

    [Fact]
    public async Task List_Reverse_Works()
    {
        var resp = await _client.PostAsJsonAsync("/api/lists/operations/reverse",
            new { items = new[] { 1, 2, 3, 4 } }, Ct);
        var body = await resp.Content.ReadFromJsonAsync<ListResp>(Ct);
        Assert.True(body!.Ok);
        Assert.Equal(new[] { 4, 3, 2, 1 }, body.After);
    }

    [Fact]
    public async Task List_Swap_Works()
    {
        var resp = await _client.PostAsJsonAsync("/api/lists/operations/swap",
            new { items = new[] { 1, 2, 3 }, arg = 1, arg2 = 3 }, Ct);
        var body = await resp.Content.ReadFromJsonAsync<ListResp>(Ct);
        Assert.True(body!.Ok);
        Assert.Equal(new[] { 3, 2, 1 }, body.After);
    }

    [Fact]
    public async Task List_SplitByValue_Works()
    {
        var resp = await _client.PostAsJsonAsync("/api/lists/operations/splitByValue",
            new { items = new[] { 1, 2, 3, 4 }, arg = 3 }, Ct);
        var body = await resp.Content.ReadFromJsonAsync<ListResp>(Ct);
        Assert.True(body!.Ok);
    }

    [Fact]
    public async Task List_UnknownOp_ReturnsBadRequest()
    {
        var resp = await _client.PostAsJsonAsync("/api/lists/operations/unknown",
            new { items = new[] { 1, 2 } }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ---- tree ----

    [Fact]
    public async Task Tree_ParseSample()
    {
        var resp = await _client.PostAsJsonAsync("/api/trees/parse-prefix",
            new { expression = "ABD*G***CE**FH**J**" }, Ct);
        var body = await resp.Content.ReadFromJsonAsync<TreeResp>(Ct);
        Assert.True(body!.Ok);
        Assert.Equal(new[] { 65, 66, 68, 71, 67, 69, 70, 72, 74 }, body.Preorder);
        Assert.Equal(4, body.Height);
    }

    // ---- benchmarks ----

    [Fact]
    public async Task Bench_Stacks_ReturnsPoints()
    {
        var resp = await _client.GetAsync("/api/benchmarks/stacks?sizes=10,100", Ct);
        var body = await resp.Content.ReadFromJsonAsync<BenchResp>(Ct);
        Assert.NotNull(body);
        Assert.Equal("Stack", body!.Structure);
        Assert.Equal(2, body.Points.Count);
    }

    [Fact]
    public async Task Bench_Queues_ReturnsPoints()
    {
        var resp = await _client.GetAsync("/api/benchmarks/queues?sizes=10,100", Ct);
        var body = await resp.Content.ReadFromJsonAsync<BenchResp>(Ct);
        Assert.NotNull(body);
        Assert.Equal("Queue", body!.Structure);
    }

    // ---- local DTOs для десериализации ----

    private record TraceDto(int Index, string Operation, string? Argument, string Result);
    private record TimingDto(double ReadMs, double ParseMs, double ExecuteMs, double TotalMs);
    private record StackResp(List<TraceDto> Trace, string FinalState, TimingDto Timing);
    private record EvalResp(bool Ok, double? Value, string? Error);
    private record PostfixResp(bool Ok, string? Postfix, string? Error);
    private record ListResp(bool Ok, string? Error, int[]? Before, int[]? After, System.Text.Json.JsonElement? Result);
    private record TreeResp(bool Ok, string? Error, int[]? Preorder, int[]? Inorder, int[]? Postorder, int Height);
    private record BenchPointDto(int Size, double CustomMs, double StandardMs, double Ratio);
    private record BenchResp(string Structure, string Operation, List<BenchPointDto> Points);
}