namespace Mustask.Application.Abstractions;

/// <summary>
/// The employee acting in the current request (every change records its
/// author). Implemented in Mustask.Api against the current
/// <c>HttpContext</c> -- Application stays unaware of ASP.NET Core or of
/// how the identity actually got there (see <c>HttpContextCurrentUser</c>
/// for the demo-mode caveat: no authentication scheme is wired yet).
/// </summary>
public interface ICurrentUser
{
    /// <summary>Display name/login of the acting employee. Never null or empty.</summary>
    string DisplayName { get; }
}
