namespace Mustask.Api.Authentication;

/// <summary>
/// The session endpoints' port to whatever records a signed-in person (tech-025, ADR-0034). Declared here so that
/// the session module (<c>Features/Session/</c>) names nothing of <c>Mustask.Domain</c> or <c>Mustask.Application</c> (ADR-0022 Decision 6); the
/// user directory implements it (<c>Features/UserDirectory/SessionUserProjection</c>) and the composition root binds the two.
/// </summary>
public interface ISessionSignInRecorder
{
    /// <summary>
    /// Records the sign-in described by <paramref name="idToken"/>. With <paramref name="knownFingerprint"/> equal to the
    /// fingerprint of the token's claims, nothing is written.
    /// </summary>
    Task<SignInRecording> RecordAsync(string? idToken, string? knownFingerprint, CancellationToken cancellationToken);
}

/// <summary>What recording a sign-in came to; what a failure means for the session is the caller's rule.</summary>
public enum SignInRecordResult
{
    /// <summary>The upsert ran (or, for a renewal, the claims had not changed since the last one).</summary>
    Recorded,

    /// <summary>The upsert failed, but the person is in the directory already.</summary>
    FailedKnownUser,

    /// <summary>The upsert failed and the person is not in the directory.</summary>
    FailedUnknownUser,
}

/// <param name="Result">What happened.</param>
/// <param name="Fingerprint">
/// The fingerprint of the claims now held in the directory, to be stored in the ticket; null when the upsert failed
/// (the next renewal tries again).
/// </param>
public sealed record SignInRecording(SignInRecordResult Result, string? Fingerprint);
