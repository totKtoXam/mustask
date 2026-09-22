namespace Mustask.Api.IntegrationTests;

// UseSerilog was once wired with
// ReadFrom.Configuration(...), but no "Serilog" section (and therefore no
// sink) existed anywhere in appsettings — the app produced zero log output.
// Proven here end-to-end rather than by reading Program.cs: a real request
// through the real app must actually write something to the console.
//
// Serilog config lives fully in appsettings*.json — this host
// runs under Development (MustaskApiFactory), so it also proves
// appsettings.Development.json's override of "Serilog:WriteTo:console"
// to Mustask.Api.Logging.DevelopmentConsoleFormatter actually took effect: the
// human-readable "[HH:mm:ss LVL] message" shape, not the structured JSON
// shape used outside Development (see ProductionLoggingTests).
//
// Runs in MustaskApiCollection, i.e. sequentially with every other class in this
// project, so the Console.Out redirection below cannot interleave with
// output from a concurrently running test in another collection.
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class LoggingTests
{
    private readonly MustaskApiFactory _factory;

    public LoggingTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    // Request-completion logging for /health and
    // /alive is now Verbose (see Program.cs's UseSerilogRequestLogging
    // options) — below the default Serilog:MinimumLevel:Default
    // (Information), so it no longer reaches the console at all (see
    // GetHealth_RequestCompletes_IsNotWrittenToConsole below). This test
    // exercises an ordinary, non-health path instead — /__test/status/200
    // (TestEndpointsStartupFilter) — to keep proving the base wiring
    // (sink configured, Development formatter applied) without depending on
    // a path that is now intentionally silenced.
    [Fact]
    public async Task GetOrdinaryRoute_RequestCompletes_WritesRequestLogToConsole()
    {
        var originalOut = Console.Out;
        var capturedOutput = new StringWriter();
        Console.SetOut(capturedOutput);
        try
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/__test/status/200");
            await response.Content.ReadAsStringAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = capturedOutput.ToString();
        Assert.False(
            string.IsNullOrWhiteSpace(output),
            "Serilog produced no console output for the request — the console sink is not wired up.");
        Assert.Contains("/__test/status/200", output);

        // DevelopmentConsoleFormatter's template opens with
        // "[HH:mm:ss LVL] " — distinct from the "{...}" JSON shape used
        // outside Development (ProductionLoggingTests). Proves the
        // appsettings.Development.json override actually applied to the
        // request-completion line itself, not just that "something" was
        // written to the console.
        var requestLine = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(line => line.Contains("/__test/status/200", StringComparison.Ordinal));
        Assert.False(string.IsNullOrEmpty(requestLine), $"No console line mentioned the request path — full output:\n{output}");
        Assert.Matches(@"^\[\d{2}:\d{2}:\d{2} \w{3}\]", requestLine);
    }

    // Health probes used to log a full
    // request-completion event on every hit — noisy once /health/alive
    // answer outside Development too (probed every few seconds by
    // infrastructure). GetLevel in Program.cs now returns Verbose for these
    // two paths specifically, which is below Serilog:MinimumLevel:Default
    // (Information) — the event never reaches the sink. This is the
    // suppression half; GetOrdinaryRoute_RequestCompletes_WritesRequestLogToConsole
    // above is the "other requests still log" half.
    [Fact]
    public async Task GetHealth_RequestCompletes_IsNotWrittenToConsole()
    {
        var originalOut = Console.Out;
        var capturedOutput = new StringWriter();
        Console.SetOut(capturedOutput);
        try
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/health");
            await response.Content.ReadAsStringAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = capturedOutput.ToString();
        Assert.DoesNotContain("responded", output, StringComparison.Ordinal);
    }
}
