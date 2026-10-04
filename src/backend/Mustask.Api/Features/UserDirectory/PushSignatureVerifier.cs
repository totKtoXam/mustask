using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Mustask.Application.Abstractions;
using Mustask.Domain.UserDirectory;
using Mustask.Shared.Exceptions;
using Microsoft.Extensions.Logging;

namespace Mustask.Api.Features.UserDirectory;

/// <summary>
/// The push channel's authentication (ADR-0025 § 3; api-contract.md B.2):
/// HMAC-SHA256, keyed by the shared secret's UTF-8 bytes, over the canonical
/// string <c>v1\n&lt;timestamp&gt;\n&lt;METHOD&gt;\n&lt;target&gt;\n&lt;hex SHA-256 of
/// the body&gt;</c>. A request is accepted when its timestamp is within
/// <see cref="Window"/> of MUSTASK's clock and its signature matches under
/// the secret or the previous secret (constant-time comparison). Any refusal
/// is one answer — 401 <c>user_directory.push_unauthorized</c> — and is
/// logged with its reason and the route, never the body, a name, an e-mail
/// or the secret.
/// </summary>
public sealed partial class PushSignatureVerifier(
    PushChannelOptions options,
    IClock clock,
    ILogger<PushSignatureVerifier> logger)
{
    public const string TimestampHeader = "Mustask-Push-Timestamp";

    public const string SignatureHeader = "Mustask-Push-Signature";

    /// <summary>The scheme version: the first line of the canonical string and the signature's prefix.</summary>
    public const string Version = "v1";

    /// <summary>How far a timestamp may be from MUSTASK's clock, either direction (api-contract.md, Part D).</summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(300);

    private const string SignaturePrefix = Version + "=";

    private const int HexSignatureLength = SHA256.HashSizeInBytes * 2;

    /// <summary>How often a request accepted under the previous secret is logged (security pass S4).</summary>
    private static readonly TimeSpan PreviousSecretWarningEvery = TimeSpan.FromHours(1);

    /// <summary>UTC ticks before which the next such warning is held back; 0 until the first.</summary>
    private long _previousSecretWarnedUntilTicks;

    /// <summary>Throws <see cref="UnauthorizedException"/> unless the request holds.</summary>
    public void EnsureValid(PushRequestParts request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var refusal = Check(request, out var acceptedUnderPreviousSecret);

        if (refusal is not null)
        {
            throw Refuse(refusal.Value, request);
        }

        if (acceptedUnderPreviousSecret)
        {
            NoteAcceptedUnderPreviousSecret();
        }
    }

    /// <summary>
    /// Throws <see cref="UnauthorizedException"/> unless what needs no body holds: both headers present and
    /// well-formed, the timestamp inside its window (<see cref="CheckEnvelope"/>). The binder runs it before it
    /// reads the body, so a request with a header missing or malformed, or a timestamp outside the window, is refused
    /// before a byte of its body is read (security pass S3). Forming those headers needs no secret: a caller who
    /// sends well-formed ones with a current timestamp still has the body read, up to
    /// <see cref="PushBodyReader.MaxBodyBytes"/>, before <see cref="EnsureValid"/> refuses the signature. This check
    /// bounds the cost of the rest; it does not authenticate (security pass S6).
    /// </summary>
    public void EnsureEnvelopeValid(PushRequestParts request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var refusal = CheckEnvelope(request, out _);

        if (refusal is not null)
        {
            throw Refuse(refusal.Value, request);
        }
    }

    /// <summary>The check alone, for tests: null when the request holds.</summary>
    public PushRefusal? Check(PushRequestParts request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Check(request, out _);
    }

    /// <summary>
    /// The checks that need no body, for tests and for <see cref="EnsureEnvelopeValid"/>: null when the headers
    /// are present and well-formed and the timestamp is inside the window.
    /// </summary>
    public PushRefusal? CheckEnvelope(PushRequestParts request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return CheckEnvelope(request, out _);
    }

    private PushRefusal? Check(PushRequestParts request, out bool acceptedUnderPreviousSecret)
    {
        acceptedUnderPreviousSecret = false;

        if (CheckEnvelope(request, out var signature) is { } refusal)
        {
            return refusal;
        }

        var canonical = CanonicalString(request.Timestamp!, request.Method, request.Target, request.Body.Span);

        if (Matches(options.Secret, canonical, signature))
        {
            return null;
        }

        if (options.PreviousSecret is { } previous && Matches(previous, canonical, signature))
        {
            acceptedUnderPreviousSecret = true;

            return null;
        }

        return PushRefusal.WrongSignature;
    }

    private PushRefusal? CheckEnvelope(PushRequestParts request, out byte[] signature)
    {
        signature = [];

        if (request.Timestamp is null && request.Signature is null)
        {
            return request.HasBearer ? PushRefusal.BearerInsteadOfSignature : PushRefusal.MissingHeaders;
        }

        if (request.Timestamp is null || request.Signature is null)
        {
            return PushRefusal.MalformedHeader;
        }

        if (!long.TryParse(request.Timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || !TryParseSignature(request.Signature, out signature))
        {
            return PushRefusal.MalformedHeader;
        }

        var now = clock.UtcNow.ToUnixTimeSeconds();

        return Math.Abs(now - seconds) > (long)Window.TotalSeconds ? PushRefusal.StaleTimestamp : null;
    }

    private UnauthorizedException Refuse(PushRefusal refusal, PushRequestParts request)
    {
        // The reason as text: a structured enum renders quoted under the
        // development console's JSON-aware template, and the reason is a word.
        LogRefusal(logger, refusal.ToString(), request.Method, request.Route);

        return new UnauthorizedException(UserDirectoryErrorCodes.PushUnauthorized);
    }

    /// <summary>
    /// A request accepted under <see cref="PushChannelOptions.PreviousSecret"/> is a Warning, at most once an hour
    /// (security pass S4): a rotation that was never finished — or a secret that leaked and was rotated away from,
    /// but left configured — would otherwise keep being accepted without a sign. Names the key, never a value.
    /// </summary>
    private void NoteAcceptedUnderPreviousSecret()
    {
        var now = clock.UtcNow.UtcTicks;
        var due = Interlocked.Read(ref _previousSecretWarnedUntilTicks);

        if (now >= due && Interlocked.CompareExchange(ref _previousSecretWarnedUntilTicks, now + PreviousSecretWarningEvery.Ticks, due) == due)
        {
            LogAcceptedUnderPreviousSecret(logger, PushChannelOptions.PreviousSecretKey);
        }
    }

    /// <summary>The canonical string of B.2: five lines joined by LF, no trailing newline.</summary>
    public static string CanonicalString(string timestamp, string method, string target, ReadOnlySpan<byte> body) =>
        string.Join(
            '\n',
            Version,
            timestamp,
            method.ToUpperInvariant(),
            target,
            Convert.ToHexStringLower(SHA256.HashData(body)));

    /// <summary>The <see cref="SignatureHeader"/> value for a canonical string — what the provider sends.</summary>
    public static string Sign(string secret, string canonical) =>
        SignaturePrefix + Convert.ToHexStringLower(Hmac(secret, canonical));

    private static bool Matches(string secret, string canonical, byte[] signature) =>
        CryptographicOperations.FixedTimeEquals(Hmac(secret, canonical), signature);

    private static byte[] Hmac(string secret, string canonical) =>
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(canonical));

    private static bool TryParseSignature(string header, out byte[] signature)
    {
        signature = [];

        if (header.Length != SignaturePrefix.Length + HexSignatureLength
            || !header.StartsWith(SignaturePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var hex = header.AsSpan(SignaturePrefix.Length);

        foreach (var ch in hex)
        {
            if (!char.IsAsciiHexDigitLower(ch))
            {
                return false;
            }
        }

        signature = Convert.FromHexString(hex);
        return true;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "User directory push refused ({Reason}) at {Method} {Route}.")]
    private static partial void LogRefusal(ILogger logger, string reason, string method, string route);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "User directory push accepted under the previous secret ({Key}): the provider has not moved to the new "
            + "secret, or the previous one was left configured after the rotation. Remove the key once the provider "
            + "uses the new secret (ADR-0025 section 3); while it stays, a leaked old secret still works.")]
    private static partial void LogAcceptedUnderPreviousSecret(ILogger logger, string key);
}

/// <summary>What the verifier reads of one request — taken by <see cref="SignedPushBody{T}"/>.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Target">The request path below <c>/api/user-directory/feed/</c>, exactly as sent.</param>
/// <param name="Route">The path, for the log line.</param>
/// <param name="Timestamp">The <see cref="PushSignatureVerifier.TimestampHeader"/> value, or null.</param>
/// <param name="Signature">The <see cref="PushSignatureVerifier.SignatureHeader"/> value, or null.</param>
/// <param name="HasBearer">Whether an <c>Authorization: Bearer</c> header came instead.</param>
/// <param name="Body">The raw body bytes.</param>
public sealed record PushRequestParts(
    string Method,
    string Target,
    string Route,
    string? Timestamp,
    string? Signature,
    bool HasBearer,
    ReadOnlyMemory<byte> Body);
