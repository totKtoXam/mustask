using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Microsoft.AspNetCore.Authorization;

namespace Mustask.Api.Authorization;

/// <summary>
/// The scoped check, per request (ADR-0027 rule 3, plan-backend § 3.5):
/// <list type="number">
/// <item>an unauthenticated user gets no verdict: the policy's other requirement fails and the middleware challenges, so 401
/// comes before anything else;</item>
/// <item>the <c>organizationId</c> of the route, parsed with <see cref="AddressedId"/>; malformed fails with
/// <see cref="OrganizationAccessFailureKind.OrganizationNotFound"/>;</item>
/// <item>one read of the organization's status joined to the caller's membership (the caller is the token's <c>sub</c>) —
/// no cache beyond the request, no call to Keycloak, no read of the user projection (BR-M1-32);</item>
/// <item>no such organization, one that is not active (unless the route accepts a deactivated one), or no membership: the
/// <b>same</b> reason for every cause, so the answer is the same bytes;</item>
/// <item>the role's bundle lacks the route's code: <see cref="OrganizationAccessFailureKind.RoleLacksCode"/>;</item>
/// <item>otherwise the resolved role is kept for the request (<see cref="HttpContextOrganizationAccess"/>) and the
/// requirement succeeds.</item>
/// </list>
/// </summary>
/// <remarks>
/// It lives in the authorization middleware, not in an endpoint filter: ASP.NET Core binds parameters before it runs a
/// filter, so a filter could not give a non-member with a malformed body the 404 the spec orders first (401, 404, 403,
/// then the rest). The handler is scoped: it uses the request's database context.
/// </remarks>
public sealed class OrganizationPermissionHandler(
    IOrganizationAccessReadModel accessReadModel,
    ICurrentUser currentUser) : AuthorizationHandler<OrganizationPermissionRequirement>
{
    /// <summary>The route parameter every organization-addressed route names (mod-015 BR-18).</summary>
    public const string RouteParameter = "organizationId";

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, OrganizationPermissionRequirement requirement)
    {
        if (context.User.Identity is not { IsAuthenticated: true })
        {
            return;
        }

        var httpContext = context.Resource as HttpContext
            ?? throw new InvalidOperationException("The organization permission requirement is evaluated on an HTTP request only.");

        var raw = httpContext.Request.RouteValues[RouteParameter] as string;

        if (!AddressedId.TryParse(raw, out var organizationId))
        {
            context.Fail(new OrganizationAccessFailure(this, OrganizationAccessFailureKind.OrganizationNotFound, "malformed", requirement.Code));

            return;
        }

        var row = await accessReadModel.FindAsync(organizationId, currentUser.SubjectId, httpContext.RequestAborted);

        var accepted = row?.Status switch
        {
            OrganizationStatus.Active => true,
            OrganizationStatus.Deactivated => requirement.AcceptsDeactivated,
            _ => false,
        };

        if (row is not { Role: { } role } || !accepted)
        {
            context.Fail(new OrganizationAccessFailure(
                this, OrganizationAccessFailureKind.OrganizationNotFound, organizationId.ToString(), requirement.Code));

            return;
        }

        if (!OrganizationRoleBundles.Holds(role, requirement.Code))
        {
            context.Fail(new OrganizationAccessFailure(
                this, OrganizationAccessFailureKind.RoleLacksCode, organizationId.ToString(), requirement.Code));

            return;
        }

        httpContext.Items[HttpContextOrganizationAccess.ItemKey] = new ResolvedOrganizationAccess(organizationId, role);
        context.Succeed(requirement);
    }
}

/// <summary>What the scoped check resolved for one request: the organization of the address and the caller's role in it.</summary>
internal sealed record ResolvedOrganizationAccess(Guid OrganizationId, OrganizationRole Role);
