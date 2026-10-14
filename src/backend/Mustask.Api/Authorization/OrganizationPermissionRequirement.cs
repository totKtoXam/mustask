using Microsoft.AspNetCore.Authorization;

namespace Mustask.Api.Authorization;

/// <summary>
/// The scoped check of ADR-0027 as an authorization requirement: the caller must hold a membership in the organization of
/// the address whose role bundle holds <paramref name="Code"/>. It is evaluated by the authorization middleware — before
/// the request body is read, in the place every access decision already lives — by
/// <see cref="OrganizationPermissionHandler"/>, and its failures are rendered as the business 404 and 403 by
/// <see cref="ForbiddenErrorCodeResultHandler"/>.
/// </summary>
/// <param name="Code">An organization code of the catalog (<c>PermissionScope.Organization</c>).</param>
/// <param name="AcceptsDeactivated">
/// <c>true</c> for the one request a deactivated organization accepts, its restore (api-contract § 4.1 row 7). Every other
/// route answers not found for a deactivated organization.
/// </param>
public sealed record OrganizationPermissionRequirement(string Code, bool AcceptsDeactivated) : IAuthorizationRequirement;
