namespace Mustask.Api.Features.Session;

/// <summary>
/// The renewed id token failed <see cref="RenewedIdTokenValidator"/> —
/// bad signature/issuer/audience/lifetime, or a <c>sub</c>/<c>sid</c> that
/// does not match the ticket. Internal, like
/// <see cref="RefreshGrantRejectedException"/>: <c>POST /api/session/token</c>
/// catches it itself and turns it into <c>session.expired</c> after clearing
/// the cookie — it never crosses the endpoint boundary as-is.
/// </summary>
internal sealed class RenewedIdTokenRejectedException(string reason, Exception? inner = null)
    : Exception(reason, inner)
{
    /// <summary>Safe to log — names the failed check, never the token.</summary>
    public string Reason { get; } = reason;
}
