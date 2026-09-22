using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;

namespace Mustask.Api.IntegrationTests;

// Serilog sink
// configuration used to be hardcoded in Program.cs (plain WriteTo.Console(),
// same shape in every environment). It now comes entirely from
// appsettings*.json: appsettings.json (the effective config outside
// Development, e.g. Production) configures a structured JSON console sink
// (CompactJsonFormatter); appsettings.Development.json overrides only the
// formatter to a human-readable one (see LoggingTests, which proves the
// Development shape). This proves the *other* half end-to-end — a real
// request through a Production host must produce a JSON line on the
// console, not the bracketed "[HH:mm:ss LVL] message" shape used in dev.
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ProductionLoggingTests
{
    private readonly MustaskApiFactory _factory;

    public ProductionLoggingTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    // /health's request-completion log is now
    // Verbose (Program.cs, UseSerilogRequestLogging options) — below
    // Serilog:MinimumLevel:Default (Information), so it no longer reaches
    // the console. Exercising an ordinary path instead
    // (/__test/status/200, TestEndpointsStartupFilter — still mapped under
    // this Production-derived factory, see ApiExplorerTests) keeps this test
    // proving the *rest* of the wiring (JSON sink, enrichers) end-to-end.
    // GetHealth_ProductionRequestCompletes_IsNotWrittenToConsole below covers
    // the suppression itself.
    [Fact]
    public async Task GetOrdinaryRoute_ProductionRequestCompletes_WritesJsonRequestLogToConsole()
    {
        var originalOut = Console.Out;
        var capturedOutput = new StringWriter();
        Console.SetOut(capturedOutput);
        try
        {
            using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
            var client = factory.CreateClient();
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

        var requestLine = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(line => line.Contains("/__test/status/200", StringComparison.Ordinal));
        Assert.False(
            string.IsNullOrEmpty(requestLine),
            $"No console line mentioned the request path — full output:\n{output}");

        // The Development console format (LoggingTests) opens with
        // "[HH:mm:ss LVL] ..." — the shapes are mutually exclusive, so this
        // also proves Production did not accidentally pick up the
        // Development formatter (or vice versa).
        Assert.StartsWith("{", requestLine, StringComparison.Ordinal);

        // Parses as valid, well-formed JSON — not just text that happens to
        // start with "{".
        var parsed = JsonNode.Parse(requestLine)!.AsObject();
        Assert.True(parsed.ContainsKey("@t"), "Compact JSON events always carry a \"@t\" timestamp field.");

        // appsettings.json's "Enrich" list
        // (FromLogContext/WithMachineName/WithEnvironmentName/WithThreadId)
        // and "Properties" were only asserted by an uncommitted scratch
        // console project during development — no automated test proved the
        // enrichers actually ran inside the real host. CompactJsonFormatter
        // renders enricher/static properties as top-level keys named after
        // the enricher (e.g. "MachineName"), so this pins that contract down
        // for real, on a real request through the real DI-configured host.
        Assert.True(parsed.ContainsKey("MachineName"), $"Missing \"MachineName\" (WithMachineName enricher) — full line:\n{requestLine}");
        Assert.True(parsed.ContainsKey("EnvironmentName"), $"Missing \"EnvironmentName\" (WithEnvironmentName enricher) — full line:\n{requestLine}");
        Assert.True(parsed.ContainsKey("ThreadId"), $"Missing \"ThreadId\" (WithThreadId enricher) — full line:\n{requestLine}");
        Assert.Equal("Mustask.Api", (string?)parsed["Application"]);
    }

    // Suppression half — see LoggingTests'
    // Development counterpart for the same assertion under that environment.
    [Fact]
    public async Task GetHealth_ProductionRequestCompletes_IsNotWrittenToConsole()
    {
        var originalOut = Console.Out;
        var capturedOutput = new StringWriter();
        Console.SetOut(capturedOutput);
        try
        {
            using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
            var client = factory.CreateClient();
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
