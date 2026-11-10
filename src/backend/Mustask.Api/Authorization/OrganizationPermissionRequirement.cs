using Mustask.Domain.Identity;
using Microsoft.AspNetCore.Authorization;

namespace Mustask.Api.Authorization;

/// <summary>
/// The scoped check of ADR-0027 as an authorization requirement: the caller must hold a membership in the organization of
/// the address whose role bundle holds <paramref name="Code"/>. It is evaluated by the authorization middleware — before
/// the request body is read, in the place every access decision already lives — by
/// <see cref="OrganizationPermissionHandler"/>, and its failures are rendered as the business 404 and 403 by
/// <see cref="ForbiddenErrorCodeResultHandler"/>.
/// </summary>
/// <param name="Code">An organization code of the catalog (<c>PermissionSource.Organization</c>).</param>
/// <param name="AcceptsDeactivated">
/// <c>true</c> for the one request a deactivated organization accepts, its restore (api-contract § 4.1 row 7). Every other
/// route answers not found for a deactivated organization.
/// </param>
/// <param name="Scope">
/// The kind of object the route addresses (mod-019, ADR-0029 § 7): for a code of another resource scope than the organization's the
/// grants that reach that object are added to the role's bundle. <see cref="ResourceScope.Organization"/> is today's check.
/// </param>
/// <param name="MembershipOnly">
/// <c>true</c> for a route whose code is decided in its handler because the addressed object is carried in the body (the place of a
/// new project or area, mod-019 BR-SA-17): the check only proves an active organization and a membership, and the handler answers 403.
/// </param>
public sealed record OrganizationPermissionRequirement(
    string Code, bool AcceptsDeactivated, ResourceScope Scope = ResourceScope.Organization, bool MembershipOnly = false)
    : IAuthorizationRequirement;
