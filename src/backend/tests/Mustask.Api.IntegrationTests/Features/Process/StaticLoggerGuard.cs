using Serilog;

namespace Mustask.Api.IntegrationTests.Features.Process;

/// <summary>
/// Puts Serilog's static <c>Log.Logger</c> back when a test is done with a host of its own.
/// </summary>
/// <remarks>
/// <para>
/// <c>Program.cs</c> builds its Serilog logger per host and, as <c>UseSerilog</c> is wired
/// there, assigns it to the static <c>Log.Logger</c> on every <c>Build()</c>. A host that
/// <c>WithWebHostBuilder</c> derives from the shared factory therefore replaces the static
/// logger with its own, and disposing that host disposes it. From then on the shared host's
/// <c>ILogger&lt;T&gt;</c> instances, which are created per request and bind to the static
/// logger at that moment, write into a disposed logger and are silent. A later test that
/// captures the console output of a DI logger then fails for a reason that has nothing to do
/// with it (found: <c>SessionLogoutEndpointTests.Logout_UnexpectedKeycloakError_...</c> failed
/// as soon as a process test had disposed a derived host before it; its request-log line,
/// which uses a logger pinned at host build, still appeared).
/// </para>
/// <para>
/// The guard restores whatever was in place before the test. Declare it before the derived
/// host, so that it is disposed after it:
/// <c>using var guard = new StaticLoggerGuard(); using var host = factory.WithWebHostBuilder(...);</c>
/// </para>
/// </remarks>
internal sealed class StaticLoggerGuard : IDisposable
{
    private readonly ILogger _before = Log.Logger;

    public void Dispose() => Log.Logger = _before;
}
