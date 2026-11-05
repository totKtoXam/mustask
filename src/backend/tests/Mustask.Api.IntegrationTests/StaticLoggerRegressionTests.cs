using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mustask.Api.IntegrationTests;

// tech-006: Program.cs used to call UseSerilog without preserveStaticLogger, so disposing a derived host ran
// Log.CloseAndFlush() on the process-wide logger, and ILogger<T> instances bound to it were silent: log-line
// assertions passed vacuously or failed depending on test order. This test pins the fix. It builds the
// shared host FIRST (the old failure needs it to exist before the child; otherwise the shared host is built
// after the dispose and gets a live logger, and the test passes on the old wiring). It captures Console.Out,
// so it runs alone (ApiTestCollectionFactory.SerialClasses).
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class StaticLoggerRegressionTests
{
    private readonly MustaskApiFactory _factory;

    public StaticLoggerRegressionTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SharedHostLogger_CreatedAfterChildHostDisposed_StillWritesToConsole()
    {
        _ = _factory.Services; // the shared host must exist before the child is built and disposed

        using (var child = _factory.WithWebHostBuilder(_ => { }))
        {
            using var response = await child.CreateClient().GetAsync("/__test/status/200");
            Assert.True(response.IsSuccessStatusCode);
        }

        // Serilog closes the static asynchronously, shortly after Dispose() returns (bug-002 measured under
        // 10 ms); wait well past that so the old behaviour cannot hide behind the race.
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        // A category nobody has used, so the logger is created now, after the dispose.
        var marker = $"tech-006-marker-{Guid.NewGuid():N}";
        var logger = _factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger($"Tech006.{marker}");

        Assert.Contains(marker, WriteAndCapture(logger, marker), StringComparison.Ordinal);
    }

    private static string WriteAndCapture(ILogger logger, string marker)
    {
        var originalOut = Console.Out;
        var capturedOutput = new StringWriter();
        Console.SetOut(capturedOutput);
        try
        {
            logger.LogError("{Marker}", marker);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        return capturedOutput.ToString();
    }
}
