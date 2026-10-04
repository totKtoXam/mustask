using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Mustask.Api.Features.UserDirectory;
using Mustask.Application.Abstractions;
using Mustask.Shared.Exceptions;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// QA re-check 3 (qa.md, RC3-20, RC3-25..RC3-28): the security pass's S3 and S4 fixes on the verifier alone — no host,
/// no database, a settable clock. The early envelope check shares the window with the full check, to the second; the
/// warning for a request accepted under <c>PreviousSecret</c> comes at most once an hour even under concurrency and a
/// clock that steps back, and never for the current secret, a refused request, or a secret configured twice; the
/// startup notice names the key and never a value; and an empty or missing <c>PreviousSecret</c> accepts nothing.
/// </summary>
public sealed class PushSecretAuditQaTests
{
    private const string Current = "the-current-secret-0123456789abcdef-0123456789";

    private const string Previous = "the-previous-secret-0123456789abcdef-0123456789";

    private const string Third = "a-third-secret-nobody-configured-0123456789abcdef";

    private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds(1_790_000_000);

    private static readonly byte[] Body = Encoding.UTF8.GetBytes("""{"changes":[]}""");

    private static PushSignatureVerifier Verifier(SettableClock clock, RecordingLogger<PushSignatureVerifier> logger, string? previous = Previous, string current = Current) =>
        new(new PushChannelOptions { Secret = current, PreviousSecret = previous }, clock, logger);

    private static PushRequestParts Signed(DateTimeOffset at, string secret, bool bearer = false)
    {
        var timestamp = at.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = PushSignatureVerifier.Sign(secret, PushSignatureVerifier.CanonicalString(timestamp, "POST", "changes", Body));

        return new PushRequestParts("POST", "changes", "/api/user-directory/feed/changes", timestamp, signature, bearer, Body);
    }

    private static int PreviousSecretWarnings(RecordingLogger<PushSignatureVerifier> logger) =>
        logger.Lines.Count(line => line.Level == LogLevel.Warning && line.Text.Contains(PushChannelOptions.PreviousSecretKey, StringComparison.Ordinal));

    // RC3-20: the early step's window is the full check's, to the second, either way — on a fixed clock.
    [Theory]
    [InlineData(-301, false)]
    [InlineData(-300, true)]
    [InlineData(0, true)]
    [InlineData(300, true)]
    [InlineData(301, false)]
    public void TheEnvelopeCheck_HasTheSameWindowAsTheFullCheck(int offsetSeconds, bool inside)
    {
        var verifier = Verifier(new SettableClock(Start), new RecordingLogger<PushSignatureVerifier>());
        var request = Signed(Start.AddSeconds(offsetSeconds), Third);

        Assert.Equal(inside ? null : PushRefusal.StaleTimestamp, verifier.CheckEnvelope(request));

        // The full check agrees on the envelope: inside the window only the signature can still fail (this one is third-secret).
        Assert.Equal(inside ? PushRefusal.WrongSignature : PushRefusal.StaleTimestamp, verifier.Check(request));
    }

    // RC3-20: a good envelope is no refusal and no log line; a bad one is one Warning line with its reason, method and
    // route — no header value, no secret.
    [Fact]
    public void TheEnvelopeStep_LogsNothingForAGoodEnvelope_AndOneLineWithReasonAndRouteForABadOne()
    {
        var clock = new SettableClock(Start);
        var logger = new RecordingLogger<PushSignatureVerifier>();
        var verifier = Verifier(clock, logger);

        verifier.EnsureEnvelopeValid(Signed(Start, Third));
        Assert.Empty(logger.Lines);

        var stale = Signed(Start.AddSeconds(-1000), Current);
        Assert.Throws<UnauthorizedException>(() => verifier.EnsureEnvelopeValid(stale));

        var line = Assert.Single(logger.Lines);
        Assert.Equal(LogLevel.Warning, line.Level);
        Assert.Contains("StaleTimestamp", line.Text, StringComparison.Ordinal);
        Assert.Contains("/api/user-directory/feed/changes", line.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(stale.Signature!, line.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(stale.Timestamp!, line.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Current, line.Text, StringComparison.Ordinal);
    }

    // RC3-25: the Warning names the key and neither secret nor any part of one.
    [Fact]
    public void TheWarning_NamesTheKey_AndCarriesNoSecretOrPrefixOfOne()
    {
        var logger = new RecordingLogger<PushSignatureVerifier>();
        var verifier = Verifier(new SettableClock(Start), logger);

        verifier.EnsureValid(Signed(Start, Previous));

        var line = Assert.Single(logger.Lines);
        Assert.Contains(PushChannelOptions.PreviousSecretKey, line.Text, StringComparison.Ordinal);

        foreach (var secret in new[] { Current, Previous })
        {
            for (var length = 8; length <= secret.Length; length += 4)
            {
                Assert.DoesNotContain(secret[..length], line.Text, StringComparison.Ordinal);
            }
        }
    }

    // RC3-26: none of these is a request accepted under the previous secret.
    [Fact]
    public void NoWarning_ForTheCurrentSecret_ARefusal_AThirdSecret_OrASecretConfiguredTwice()
    {
        var clock = new SettableClock(Start);
        var logger = new RecordingLogger<PushSignatureVerifier>();
        var verifier = Verifier(clock, logger);

        verifier.EnsureValid(Signed(Start, Current));
        Assert.Throws<UnauthorizedException>(() => verifier.EnsureValid(Signed(Start, Third)));
        Assert.Throws<UnauthorizedException>(() => verifier.EnsureValid(Signed(Start.AddSeconds(-1000), Previous)));
        Assert.Throws<UnauthorizedException>(() => verifier.EnsureValid(Signed(Start, Previous, bearer: false) with { Signature = null }));

        Assert.Equal(0, PreviousSecretWarnings(logger));

        var sameTwice = new RecordingLogger<PushSignatureVerifier>();
        var twice = Verifier(clock, sameTwice, previous: Current);
        twice.EnsureValid(Signed(Start, Current));
        Assert.Equal(0, PreviousSecretWarnings(sameTwice));
    }

    // RC3-27: fifty requests at once, over and over: one Warning for each fresh verifier, never two.
    [Fact]
    public async Task FiftyConcurrentRequestsUnderThePreviousSecret_WarnExactlyOnce_InTwoHundredRounds()
    {
        for (var round = 0; round < 200; round++)
        {
            var logger = new RecordingLogger<PushSignatureVerifier>();
            var verifier = Verifier(new SettableClock(Start), logger);
            var request = Signed(Start, Previous);
            using var gate = new ManualResetEventSlim(false);

            var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
            {
                gate.Wait();
                verifier.EnsureValid(request);
            })).ToArray();

            gate.Set();
            await Task.WhenAll(tasks);

            Assert.True(PreviousSecretWarnings(logger) == 1, $"round {round}: {PreviousSecretWarnings(logger)} warnings");
        }
    }

    // RC3-27: a clock that steps back (an NTP correction) does not make every request warn; the hour counts from the
    // first Warning, and the next one comes when the clock is an hour past it.
    [Fact]
    public void AClockThatStepsBack_DoesNotMakeEveryRequestWarn_AndTheNextWarningComesAnHourAfterTheFirst()
    {
        var clock = new SettableClock(Start);
        var logger = new RecordingLogger<PushSignatureVerifier>();
        var verifier = Verifier(clock, logger);

        verifier.EnsureValid(Signed(Start, Previous));
        Assert.Equal(1, PreviousSecretWarnings(logger));

        clock.Set(Start.AddHours(-2));

        for (var i = 0; i < 5; i++)
        {
            verifier.EnsureValid(Signed(clock.UtcNow, Previous));
        }

        Assert.Equal(1, PreviousSecretWarnings(logger));

        clock.Set(Start.AddMinutes(61));
        verifier.EnsureValid(Signed(clock.UtcNow, Previous));
        Assert.Equal(2, PreviousSecretWarnings(logger));
    }

    // RC3-28: the startup notice, alone: one Information line naming the key when configured, none when not.
    [Fact]
    public async Task TheStartupNotice_NamesTheKeyOnce_WhenConfigured_AndNothingWhenNot()
    {
        var configured = new RecordingLogger<PushChannelStartupNotice>();
        await new PushChannelStartupNotice(new PushChannelOptions { Secret = Current, PreviousSecret = Previous }, configured).StartAsync(CancellationToken.None);

        var line = Assert.Single(configured.Lines);
        Assert.Equal(LogLevel.Information, line.Level);
        Assert.Contains(PushChannelOptions.PreviousSecretKey, line.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Previous, line.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Current, line.Text, StringComparison.Ordinal);

        var none = new RecordingLogger<PushChannelStartupNotice>();
        await new PushChannelStartupNotice(new PushChannelOptions { Secret = Current }, none).StartAsync(CancellationToken.None);
        Assert.Empty(none.Lines);
    }

    // RC3-28: what configuration turns into an option: an empty or missing previous secret is none, a short one stops
    // the start naming the key and not the value, a short current one too.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AnEmptyOrMissingPreviousSecret_IsNone(string? configured)
    {
        var options = PushChannelOptions.FromConfiguration(Configuration(Current, configured));

        Assert.Null(options.PreviousSecret);
    }

    [Fact]
    public void AShortPreviousSecret_StopsTheStart_NamingTheKey_NotTheValue()
    {
        var shortOne = "too-short-secret-value";
        var ex = Assert.Throws<InvalidOperationException>(() => PushChannelOptions.FromConfiguration(Configuration(Current, shortOne)));

        Assert.Contains(PushChannelOptions.PreviousSecretKey, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(shortOne, ex.Message, StringComparison.Ordinal);

        var shortCurrent = Assert.Throws<InvalidOperationException>(() => PushChannelOptions.FromConfiguration(Configuration(shortOne, null)));
        Assert.Contains(PushChannelOptions.SecretKey, shortCurrent.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(shortOne, shortCurrent.Message, StringComparison.Ordinal);
    }

    // RC3-28: with no previous secret an empty key is not a way in — a request signed with "" is a wrong signature.
    [Fact]
    public void ASignatureMadeWithAnEmptyKey_IsRefused_WhetherOrNotAPreviousSecretIsConfigured()
    {
        foreach (var previous in new string?[] { null, Previous })
        {
            var verifier = Verifier(new SettableClock(Start), new RecordingLogger<PushSignatureVerifier>(), previous);

            Assert.Equal(PushRefusal.WrongSignature, verifier.Check(Signed(Start, string.Empty)));
        }
    }

    private static IConfiguration Configuration(string secret, string? previous) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [PushChannelOptions.SecretKey] = secret,
                [PushChannelOptions.PreviousSecretKey] = previous,
            })
            .Build();

    private sealed class SettableClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = now;

        public void Set(DateTimeOffset to) => UtcNow = to;
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly object _gate = new();

        private readonly List<(LogLevel Level, string Text)> _lines = [];

        public IReadOnlyList<(LogLevel Level, string Text)> Lines
        {
            get
            {
                lock (_gate)
                {
                    return [.. _lines];
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_gate)
            {
                _lines.Add((logLevel, formatter(state, exception)));
            }
        }
    }
}
