namespace Mustask.Application.Abstractions;

/// <summary>
/// The employee acting in the current request (every change records its
/// author). Implemented in Mustask.Api against the current
/// <c>HttpContext</c> -- Application stays unaware of ASP.NET Core or of
/// how the identity actually got there (see <c>HttpContextCurrentUser</c>).
/// </summary>
public interface ICurrentUser
{
    /// <summary>Display name/login of the acting employee. Never null or empty.</summary>
    string DisplayName { get; }

    /// <summary>
    /// The token's subject (<c>sub</c>): the stable key of the user that
    /// mod-014 and mod-016 use, and the creator record of an organization or a
    /// project (mod-002 BR-M1-21). Read only on an authenticated request; there
    /// it is never null or empty, because Keycloak always sets it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The request is not authenticated, or its token carries no subject.</exception>
    string SubjectId { get; }
}
