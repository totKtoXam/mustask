namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// Captures what a host's Serilog console sink writes while an action runs
/// (the technique of <c>LoggingTests</c>). Safe only for a class in
/// <see cref="ApiTestCollectionFactory.SerialClasses"/>, which runs alone: a
/// new caller must be listed there, or it captures other shards' lines.
/// </summary>
/// <remarks>
/// The action talks to a host of its own, built inside the capture. Why:
/// <c>UseSerilog(...)</c> in <c>Program.cs</c> does not preserve the static
/// logger, so each <c>ILogger&lt;T&gt;</c> binds to whatever
/// <c>Serilog.Log.Logger</c> is current when it is first resolved — in the
/// full suite, for the shared host, often a logger another (Production-format
/// or already disposed) host left behind, so the shared host's push loggers
/// may write nowhere. A host built here resolves its loggers while its own
/// logger is the static one (the same effect <c>Program.cs</c> pins away for
/// request logging). It shares the factory's database.
/// </remarks>
internal static class ConsoleCapture
{
    public static async Task<string> RunAsync(MustaskApiFactory factory, Func<PushTestClient, Task> action)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(action);

        var originalOut = Console.Out;
        using var captured = new StringWriter();
        Console.SetOut(captured);

        try
        {
            using var host = factory.WithWebHostBuilder(_ => { });
            using var client = host.CreateClient();

            await action(new PushTestClient(client));
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        return captured.ToString();
    }
}
