using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mustask.Api.IntegrationTests;

// tech-006: Program.cs used to call UseSerilog without preserveStaticLogger, so disposing ANY derived host ran
// Log.CloseAndFlush() on the process-wide logger, and every ILogger<T> created on a sibling host afterwards was
// bound to a silent logger: log-line assertions passed vacuously or failed depending on test order. This class
// pins the fix: a line written through a logger created on the shared host after a child host was disposed must
// still reach the console. It captures Console.Out, so it runs alone (ApiTestCollectionFactory.SerialClasses).
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
        // Build, use and dispose a child host. On the old wiring this closed the static Log.Logger.
        using (var child = _factory.WithWebHostBuilder(_ => { }))
        {
            using var response = await child.CreateClient().GetAsync("/__test/status/200");
            Assert.True(response.IsSuccessStatusCode);
        }

        // Serilog swaps and closes the static asynchronously, shortly after Dispose() returns (measured in
        // bug-002 at under 10 ms); wait well past that so the old behaviour cannot hide behind the race.
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        // A category nobody has used, so the logger is created now, after the dispose.
        var marker = $"tech-006-marker-{Guid.NewGuid():N}";
        var logger = _factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger($"Tech006.{marker}");

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

        Assert.Contains(marker, capturedOutput.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChildHostDispose_DoesNotSilenceTheLoggerOfALiveSiblingHost()
    {
        // The logger exists BEFORE the dispose: the other half of the old bug (a disposed host closed the logger
        // that a live host was still writing through).
        var marker = $"tech-006-live-{Guid.NewGuid():N}";
        var logger = _factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger($"Tech006.{marker}");

        using (var child = _factory.WithWebHostBuilder(_ => { }))
        {
            using var response = await child.CreateClient().GetAsync("/__test/status/200");
            Assert.True(response.IsSuccessStatusCode);
        }

        await Task.Delay(TimeSpan.FromMilliseconds(500));

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

        Assert.Contains(marker, capturedOutput.ToString(), StringComparison.Ordinal);
    }
}
