using Mustask.Shared.Errors;

namespace Mustask.Api.Features.Session;

/// <summary>
/// Session module error codes — registry per ADR-0005, texts in
/// <c>contracts/errors/session.json</c>. <c>[ErrorCodes]</c> is what makes
/// <c>Mustask.Contracts.Tests</c> see these constants at all (docs/conventions/backend.md,
/// "A class holding code constants is marked [ErrorCodes]") — a forgotten
/// attribute is a green build and a faceless 500 at runtime instead of the
/// expected 401/400.
/// </summary>
[ErrorCodes]
public static class SessionErrorCodes
{
    /// <summary>No <c>mustask.session</c> cookie at all — never signed in, or already signed out. 401.</summary>
    public const string Anonymous = "session.anonymous";

    /// <summary>Cookie present, but Keycloak rejected the refresh grant (<c>invalid_grant</c>). 401.</summary>
    public const string Expired = "session.expired";

    /// <summary><c>returnUrl</c> is not a local path. 400.</summary>
    public const string InvalidReturnUrl = "session.invalid_return_url";
}
