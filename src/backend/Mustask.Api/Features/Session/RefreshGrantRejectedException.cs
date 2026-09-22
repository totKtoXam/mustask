namespace Mustask.Api.Features.Session;

/// <summary>
/// Keycloak's token endpoint answered <c>400 invalid_grant</c> to a
/// <c>grant_type=refresh_token</c> request — the refresh token is dead
/// (rotation reuse, revocation, or the session expired). Internal, not an
/// <see cref="Mustask.Shared.Exceptions.MustaskException"/>: <c>POST /api/session/token</c>
/// catches it itself and turns it into <c>session.expired</c> after clearing
/// the cookie — it never crosses the endpoint
/// boundary as-is.
/// </summary>
internal sealed class RefreshGrantRejectedException : Exception;
