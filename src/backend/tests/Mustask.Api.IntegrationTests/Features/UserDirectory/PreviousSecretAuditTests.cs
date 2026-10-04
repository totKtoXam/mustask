using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Mustask.Api.Features.UserDirectory;
using Mustask.Application.Abstractions;
using static Mustask.Api.IntegrationTests.Features.UserDirectory.UserDirectoryTestData;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// mod-014 security pass S4 (review.md, "Security pass — backend"): a secret
/// that is accepted without anyone seeing it is a leak that does not end. While
/// <c>UserDirectory:Push:PreviousSecret</c> is configured the log says so at
/// startup (Information), and a request accepted under it is a Warning — once
/// per hour, so a rotation that was never finished stays visible without a line
/// per push. Both name the key, never a value. ADR-0025 § 3, rotation.
/// </summary>
[Collection(MustaskApiCollection.Name)]
public sealed class PreviousSecretAuditTests(MustaskApiFactory factory) : IAsyncLifetime
{
    private const string Previous = "the-previous-audit-secret-0123456789abcdef";

    private const string EnvironmentKey = "UserDirectory__Push__PreviousSecret";

    private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds(1_790_000_000);

    public Task InitializeAsync() => ResetAsync(factory);

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Runs <paramref name="action"/> against a host whose configuration holds <see cref="Previous"/>.</summary>
    private async Task<string> RunWithPreviousSecretAsync(Func<PushTestClient, Task> action)
    {
        Environment.SetEnvironmentVariable(EnvironmentKey, Previous);

        try
        {
            return await ConsoleCapture.RunAsync(factory, action);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentKey, null);
        }
    }

    private static async Task SendAsync(PushTestClient push, string secret)
    {
        var body = PushTestClient.ChangesBody(Push(NewId(), "Анна", "Ключева", null));
        using var response = await push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", body, secret: secret));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string[] Lines(string output, string level, string text) =>
        [.. output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Contains(level + "]", StringComparison.Ordinal) && line.Contains(text, StringComparison.Ordinal))];

    private static void AssertNoSecretPrinted(string output)
    {
        Assert.DoesNotContain(Previous, output, StringComparison.Ordinal);
        Assert.DoesNotContain(MustaskApiFactory.TestPushSecret, output, StringComparison.Ordinal);
    }

    // S4: the operator is told at startup that a second secret is accepted.
    [Fact]
    public async Task APreviousSecretConfigured_IsAnnouncedAtStartup_ByItsKey_NotItsValue()
    {
        var output = await RunWithPreviousSecretAsync(_ => Task.CompletedTask);

        var lines = Lines(output, "INF", PushChannelOptions.PreviousSecretKey);
        Assert.Single(lines);
        Assert.Contains("accepted", lines[0], StringComparison.Ordinal);
        AssertNoSecretPrinted(output);
    }

    [Fact]
    public async Task NoPreviousSecretConfigured_NothingIsAnnounced()
    {
        var output = await ConsoleCapture.RunAsync(factory, push => SendAsync(push, MustaskApiFactory.TestPushSecret));

        Assert.DoesNotContain(PushChannelOptions.PreviousSecretKey, output, StringComparison.Ordinal);
    }

    // S4: a request accepted under the previous secret is a Warning naming the key — one line, however many follow.
    [Fact]
    public async Task ARequestAcceptedUnderThePreviousSecret_IsAWarning_OncePerProcess_NamingTheKey()
    {
        var output = await RunWithPreviousSecretAsync(async push =>
        {
            await SendAsync(push, Previous);
            await SendAsync(push, Previous);
            await SendAsync(push, Previous);
        });

        var warnings = Lines(output, "WRN", PushChannelOptions.PreviousSecretKey);
        Assert.Single(warnings);
        Assert.Contains("previous secret", warnings[0], StringComparison.Ordinal);
        AssertNoSecretPrinted(output);
    }

    // The provider that has moved to the new secret raises nothing, though the old one is still configured.
    [Fact]
    public async Task ARequestAcceptedUnderTheCurrentSecret_IsNoWarning_WhileAPreviousSecretIsConfigured()
    {
        var output = await RunWithPreviousSecretAsync(push => SendAsync(push, MustaskApiFactory.TestPushSecret));

        Assert.Empty(Lines(output, "WRN", PushChannelOptions.PreviousSecretKey));
        Assert.Single(Lines(output, "INF", PushChannelOptions.PreviousSecretKey));
    }

    // S4: not a line per push, not a line only once for a process that runs for months: once an hour while it keeps coming.
    [Fact]
    public void TheWarning_IsRateLimited_AnHourApart()
    {
        var clock = new SettableClock(Start);
        var logger = new RecordingLogger();
        var verifier = new PushSignatureVerifier(
            new PushChannelOptions { Secret = "the-current-secret-0123456789abcdef-0123456789", PreviousSecret = Previous },
            clock,
            logger);

        void AcceptUnderPrevious() => verifier.EnsureValid(SignedWith(clock, Previous));

        AcceptUnderPrevious();
        clock.Advance(TimeSpan.FromMinutes(59));
        AcceptUnderPrevious();
        Assert.Single(logger.Warnings);

        clock.Advance(TimeSpan.FromMinutes(2));
        AcceptUnderPrevious();
        Assert.Equal(2, logger.Warnings.Count);

        verifier.EnsureValid(SignedWith(clock, "the-current-secret-0123456789abcdef-0123456789"));
        clock.Advance(TimeSpan.FromHours(2));
        verifier.EnsureValid(SignedWith(clock, "the-current-secret-0123456789abcdef-0123456789"));
        Assert.Equal(2, logger.Warnings.Count);

        Assert.All(logger.Warnings, warning =>
        {
            Assert.Contains(PushChannelOptions.PreviousSecretKey, warning, StringComparison.Ordinal);
            Assert.DoesNotContain(Previous, warning, StringComparison.Ordinal);
        });
    }

    // The check alone stays free of side effects: only an accepted request through EnsureValid is audited.
    [Fact]
    public void TheCheckAlone_LogsNothing()
    {
        var clock = new SettableClock(Start);
        var logger = new RecordingLogger();
        var verifier = new PushSignatureVerifier(
            new PushChannelOptions { Secret = "the-current-secret-0123456789abcdef-0123456789", PreviousSecret = Previous },
            clock,
            logger);

        Assert.Null(verifier.Check(SignedWith(clock, Previous)));
        Assert.Empty(logger.Warnings);
    }

    private static PushRequestParts SignedWith(SettableClock clock, string secret)
    {
        var body = Encoding.UTF8.GetBytes("""{"changes":[]}""");
        var timestamp = clock.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = PushSignatureVerifier.Sign(secret, PushSignatureVerifier.CanonicalString(timestamp, "POST", "changes", body));

        return new PushRequestParts("POST", "changes", "/api/user-directory/feed/changes", timestamp, signature, HasBearer: false, body);
    }

    private sealed class SettableClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = now;

        public void Advance(TimeSpan by) => UtcNow += by;
    }

    private sealed class RecordingLogger : ILogger<PushSignatureVerifier>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }
    }
}
