using System.Globalization;
using System.Text;
using Mustask.Api.Features.UserDirectory;
using Mustask.Application.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// mod-014, test added at QA re-check 1 (qa.md A-14): api-contract.md Part D's
/// 300-second window at its exact edges, either direction. Over HTTP a real
/// clock ticks between signing and checking, so the edge itself is reachable
/// only on a fixed clock — the verifier's own, with no database and no host.
/// </summary>
public sealed class PushTimestampWindowTests
{
    private const string Secret = "qa-re-check-window-secret-of-at-least-32-characters";

    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_790_000_000);

    private static readonly byte[] Body = Encoding.UTF8.GetBytes("""{"changes":[]}""");

    // A timestamp exactly 300 s away is inside the window; 301 s is outside it, refused as stale rather
    // than as a wrong signature — the signature over that timestamp is right.
    [Theory]
    [InlineData(-301, false)]
    [InlineData(-300, true)]
    [InlineData(0, true)]
    [InlineData(300, true)]
    [InlineData(301, false)]
    public void TheWindow_IsThreeHundredSeconds_InclusiveEitherWay(int offsetSeconds, bool accepted)
    {
        var verifier = new PushSignatureVerifier(
            new PushChannelOptions { Secret = Secret },
            new FixedClock(Now),
            NullLogger<PushSignatureVerifier>.Instance);
        var timestamp = Now.AddSeconds(offsetSeconds).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = PushSignatureVerifier.Sign(Secret, PushSignatureVerifier.CanonicalString(timestamp, "POST", "changes", Body));

        var refusal = verifier.Check(new PushRequestParts(
            "POST", "changes", "/api/user-directory/feed/changes", timestamp, signature, HasBearer: false, Body));

        Assert.Equal(accepted ? null : PushRefusal.StaleTimestamp, refusal);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }
}
