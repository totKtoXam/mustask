using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Features.UserDirectory.RecordSignIn;
using Mustask.Domain.UserDirectory;

namespace Mustask.Api.Features.Session;

/// <summary>
/// The session endpoints' door to the user projection (tech-025, ADR-0034): turns the id token of a sign-in or of a
/// renewal into a <see cref="SignInProfile"/>, hands it to <see cref="RecordSignInHandler"/>, and applies the one rule
/// the handler leaves to its caller — what a failed upsert means for the session.
/// </summary>
/// <remarks>
/// <para>
/// <b>The failure rule.</b> A person the projection does not know cannot be added to an organization, so letting them in
/// with no row would hide the failure until someone searches for them in vain: <see cref="RecordResult.FailedUnknownUser"/>
/// fails the sign-in. A person who is in the projection already loses nothing by a failed refresh of their name:
/// <see cref="RecordResult.FailedKnownUser"/> is logged and the session goes on. A failed check of whether the person is
/// known counts as unknown, so a database outage ends a first sign-in rather than passing it unrecorded.
/// </para>
/// <para>
/// <b>The fingerprint.</b> A renewal runs every few minutes; the profile changes about never. The fingerprint of the last
/// recorded claims travels in the session cookie's ticket, and a renewal whose claims hash to the same value does not
/// touch the database.
/// </para>
/// </remarks>
public sealed class SessionUserProjection(
    RecordSignInHandler handler,
    IUserDirectory directory,
    ILogger<SessionUserProjection> logger)
{
    public enum RecordResult
    {
        /// <summary>The upsert ran (or, for a renewal, the claims had not changed since the last one).</summary>
        Recorded,

        /// <summary>The upsert failed, but the person is in the projection already.</summary>
        FailedKnownUser,

        /// <summary>The upsert failed and the person is not in the projection.</summary>
        FailedUnknownUser,
    }

    /// <param name="Result">What happened.</param>
    /// <param name="Fingerprint">
    /// The fingerprint of the claims now held in the projection, to be stored in the ticket; null when the upsert failed
    /// (the next renewal tries again).
    /// </param>
    public sealed record Recording(RecordResult Result, string? Fingerprint);

    /// <summary>
    /// Records the sign-in described by <paramref name="idToken"/>. With <paramref name="knownFingerprint"/> equal to the
    /// fingerprint of the token's claims, nothing is written.
    /// </summary>
    public async Task<Recording> RecordAsync(string? idToken, string? knownFingerprint, CancellationToken cancellationToken)
    {
        var profile = ProfileOf(idToken);
        var fingerprint = FingerprintOf(profile);

        if (knownFingerprint is not null && string.Equals(knownFingerprint, fingerprint, StringComparison.Ordinal))
        {
            return new Recording(RecordResult.Recorded, fingerprint);
        }

        try
        {
            await handler.Handle(new RecordSignInCommand(profile), cancellationToken);

            return new Recording(RecordResult.Recorded, fingerprint);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var known = await IsKnownAsync(profile.Id, cancellationToken);

            // The user id only: never a name or an e-mail, and never the token.
            logger.LogWarning(
                ex,
                "User directory: the sign-in of user {UserId} could not be recorded ({Known}).",
                profile.Id,
                known ? "the user is in the directory already" : "the user is not in the directory");

            return new Recording(known ? RecordResult.FailedKnownUser : RecordResult.FailedUnknownUser, null);
        }
    }

    private async Task<bool> IsKnownAsync(string? id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(id) || id.Length > UserDirectoryLimits.FieldMaxLength)
        {
            return false;
        }

        try
        {
            var lookup = await directory.LookupAsync([id], cancellationToken);

            return lookup.Known.ContainsKey(id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// The claims of an id token as a profile. The token comes straight from Keycloak's token endpoint (and the OIDC
    /// handler or <see cref="RenewedIdTokenValidator"/> has validated it), so it is read here, not validated again. A
    /// token that cannot be read, or a claim that is not text, gives a missing value — which
    /// <see cref="SignInProfileValidation"/> then refuses where the value is required.
    /// </summary>
    internal static SignInProfile ProfileOf(string? idToken)
    {
        if (string.IsNullOrEmpty(idToken))
        {
            return new SignInProfile(null, null, null, null, null);
        }

        JsonWebToken token;

        try
        {
            token = new JsonWebToken(idToken);
        }
        catch (ArgumentException)
        {
            return new SignInProfile(null, null, null, null, null);
        }

        return new SignInProfile(
            Claim(token, "sub"),
            Claim(token, "preferred_username"),
            Claim(token, "given_name"),
            Claim(token, "family_name"),
            Claim(token, "email"));
    }

    private static string? Claim(JsonWebToken token, string name)
    {
        try
        {
            return token.TryGetPayloadValue<string>(name, out var value) ? value : null;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    /// <summary>A short, stable digest of the claims the projection stores; not a secret and not reversible in practice.</summary>
    internal static string FingerprintOf(SignInProfile profile)
    {
        var text = string.Join(
            '\u0001',
            profile.Id,
            profile.Username,
            profile.FirstName,
            profile.LastName,
            profile.Email);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..32];
    }
}
