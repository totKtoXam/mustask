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
    IScopedAccessReadModel scopedAccess,
    ICurrentUser currentUser) : AuthorizationHandler<OrganizationPermissionRequirement>
{
    /// <summary>The route parameter every organization-addressed route names (mod-015 BR-18).</summary>
    public const string RouteParameter = "organizationId";

    private async Task<bool> HoldsAsync(
        HttpContext httpContext, OrganizationPermissionRequirement requirement, Guid organizationId, OrganizationRole role)
    {
        if (requirement.Scope == ResourceScope.Organization)
        {
            return OrganizationRoleBundles.Holds(role, requirement.Code);
        }

        var parameter = requirement.Scope switch
        {
            ResourceScope.Area => "areaId",
            ResourceScope.Project => "projectId",
            _ => "teamId",
        };

        IReadOnlyList<ReachingGrant> reaching = [];

        // An object that cannot be addressed leaves the organization role alone to answer; the handler then reports the 404.
        if (AddressedId.TryParse(httpContext.Request.RouteValues[parameter] as string, out var objectId) && role != OrganizationRole.Admin)
        {
            reaching = await scopedAccess.ReachingGrantsAsync(
                organizationId, currentUser.SubjectId, requirement.Scope, objectId, httpContext.RequestAborted) ?? [];
        }

        return EffectivePermissions.Holds(role, requirement.Scope, reaching, requirement.Code);
    }

    /// <summary>
    /// The sight gate (doc-016 BR-TD-12..-14, feat-041; ADR-0029 § 8 items 3-4): for a non-administrator, a project, then a team or area
    /// the address names, outside in, that is missing or hidden answers its own 404 before any 403. A <c>teamId</c>, <c>departmentId</c>
    /// or <c>projectId</c> of the query that names an object the caller does not see answers the same. Nothing is asked when the
    /// address names none of them. The set read here is kept for the request, so the handler's own reads do not load it twice.
    /// </summary>
    private async Task<OrganizationAccessFailure?> HiddenObjectAsync(
        HttpContext httpContext, Guid organizationId, OrganizationRole role, OrganizationPermissionRequirement requirement)
    {
        var routeValues = httpContext.Request.RouteValues;
        var query = httpContext.Request.Query;

        if (!routeValues.ContainsKey("projectId") && !routeValues.ContainsKey("teamId") && !routeValues.ContainsKey("areaId")
            && !query.ContainsKey("teamId") && !query.ContainsKey("departmentId") && !query.ContainsKey("projectId"))
        {
            return null;
        }

        var set = await scopedAccess.LoadSetAsync(organizationId, currentUser.SubjectId, role, httpContext.RequestAborted);
        httpContext.Items[HttpContextOrganizationAccess.SetKey] = set;

        OrganizationAccessFailure Hidden(string errorCode) => new(
            this, OrganizationAccessFailureKind.ObjectNotFound, organizationId.ToString(), requirement.Code, requirement.Scope, errorCode);

        // Path ids, outside in: the project, then the team or area. A malformed or unknown id is as hidden as a hidden one.
        if (routeValues["projectId"] is string project
            && !(AddressedId.TryParse(project, out var projectId) && set.SeesProject(projectId)))
        {
            return Hidden(OrganizationErrorCodes.ProjectNotFound);
        }

        if (routeValues["teamId"] is string team && !(AddressedId.TryParse(team, out var teamId) && set.SeesTeam(teamId)))
        {
            return Hidden(OrganizationErrorCodes.TeamNotFound);
        }

        if (routeValues["areaId"] is string area && !(AddressedId.TryParse(area, out var areaId) && set.SeesArea(areaId)))
        {
            return Hidden(OrganizationErrorCodes.AreaNotFound);
        }

        // Query ids (the filters of the lists and boards): one that is not a visible object of the organization (unknown, foreign, malformed
        // or hidden: indistinguishable) answers its not-found; the word `none` and an empty value are not ids.
        foreach (var value in Ids(query["projectId"]))
        {
            if (!(AddressedId.TryParse(value, out var id) && set.SeesProject(id)))
            {
                return Hidden(OrganizationErrorCodes.ProjectNotFound);
            }
        }

        foreach (var value in Ids(query["teamId"]))
        {
            if (!(AddressedId.TryParse(value, out var id) && set.SeesTeam(id)))
            {
                return Hidden(OrganizationErrorCodes.TeamNotFound);
            }
        }

        foreach (var value in Ids(query["departmentId"]))
        {
            if (!(AddressedId.TryParse(value, out var id) && set.SeesArea(id)))
            {
                return Hidden(OrganizationErrorCodes.AreaNotFound);
            }
        }

        return null;
    }

    private static IEnumerable<string> Ids(Microsoft.Extensions.Primitives.StringValues values) =>
        values.OfType<string>().Where(value => value.Length > 0 && !string.Equals(value, "none", StringComparison.OrdinalIgnoreCase));

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
            context.Fail(new OrganizationAccessFailure(this, OrganizationAccessFailureKind.OrganizationNotFound, "malformed", requirement.Code, requirement.Scope));

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
                this, OrganizationAccessFailureKind.OrganizationNotFound, organizationId.ToString(), requirement.Code, requirement.Scope));

            return;
        }

        if (role != OrganizationRole.Admin && await HiddenObjectAsync(httpContext, organizationId, role, requirement) is { } hidden)
        {
            context.Fail(hidden);

            return;
        }

        if (!requirement.MembershipOnly && !await HoldsAsync(httpContext, requirement, organizationId, role))
        {
            context.Fail(new OrganizationAccessFailure(
                this, OrganizationAccessFailureKind.RoleLacksCode, organizationId.ToString(), requirement.Code, requirement.Scope));

            return;
        }

        httpContext.Items[HttpContextOrganizationAccess.ItemKey] = new ResolvedOrganizationAccess(
            organizationId, role, requirement.Code, requirement.Scope, RefusalLog.RouteOf(httpContext));
        context.Succeed(requirement);
    }
}

/// <summary>What the scoped check resolved for one request: the organization, the caller's role in it, and the route's code and scope (for the refusal log).</summary>
internal sealed record ResolvedOrganizationAccess(
    Guid OrganizationId, OrganizationRole Role, string Code = "", ResourceScope Scope = ResourceScope.Organization, string Route = "unmatched");
