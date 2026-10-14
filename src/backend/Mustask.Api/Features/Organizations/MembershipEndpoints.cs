using Mustask.Api.Authorization;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations.AddMember;
using Mustask.Application.Features.Organizations.AddTeamMember;
using Mustask.Application.Features.Organizations.ChangeMemberRole;
using Mustask.Application.Features.Organizations.GetMembers;
using Mustask.Application.Features.Organizations.GetOrganizationAccess;
using Mustask.Application.Features.Organizations.GetTeamMembers;
using Mustask.Application.Features.Organizations.RemoveMember;
using Mustask.Application.Features.Organizations.RemoveTeamMember;
using Mustask.Application.Features.Organizations.SearchMemberCandidates;
using Mustask.Application.Features.Organizations.SearchTeamMemberCandidates;
using Mustask.Domain.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Mustask.Api.Features.Organizations;

/// <summary>
/// The access and membership routes of mod-016 (api-contract § 1 and § 2), every one mapped here and wired to its own slice.
/// Each declares its organization code with <c>RequiresOrganizationPermission</c>: the scoped check runs in the authorization
/// middleware, before the body is read, and answers 401, then 404 for a non-member (the same bytes as an organization that
/// does not exist), then 403 for a member whose role lacks the code (api-contract § 0.1). The route parameters are plain
/// strings with no constraint, so a malformed id reaches the check or the handler and gets its level's not-found code
/// instead of a routing 404 (BR-M1-20). Ids and fields in bodies are strings for the same reason.
/// </summary>
public static class MembershipEndpoints
{
    private const string Tag = "Organizations";

    private const string OrganizationPath = "/api/organizations/{organizationId}";

    private const string TeamPath = OrganizationPath + "/projects/{projectId}/teams/{teamId}";

    public static IEndpointRouteBuilder MapMembershipEndpoints(this IEndpointRouteBuilder app)
    {
        // § 1. The organization's access and members.
        var organization = app.MapGroup(OrganizationPath).WithTags(Tag);

        organization.MapGet("/access", GetOrganizationAccessAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsOrganizationRead)
            .Produces<OrganizationAccessResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        organization.MapGet("/members", GetMembersAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsMembershipRead)
            .Produces<IReadOnlyList<MemberView>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        organization.MapGet("/member-candidates", SearchMemberCandidatesAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsMembershipWrite)
            .Produces<MemberCandidatesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        organization.MapPost("/members", AddMemberAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsMembershipWrite)
            .Produces<MemberView>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        organization.MapPut("/members/{userId}/role", ChangeMemberRoleAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsMembershipWrite)
            .Produces<MemberView>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        organization.MapDelete("/members/{userId}", RemoveMemberAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsMembershipWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // § 2. A team's members. The team routes share the organization prefix, so they are mapped from the same file.
        var team = app.MapGroup(TeamPath).WithTags(Tag);

        team.MapGet("/members", GetTeamMembersAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsTeamRead)
            .Produces<IReadOnlyList<UserRefView>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        team.MapGet("/member-candidates", SearchTeamMemberCandidatesAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsTeamWrite)
            .Produces<TeamMemberCandidatesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        team.MapPost("/members", AddTeamMemberAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsTeamWrite)
            .Produces<UserRefView>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        team.MapDelete("/members/{userId}", RemoveTeamMemberAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsTeamWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    // --- § 1. Access and members ----------------------------------------------------

    private static async Task<IResult> GetOrganizationAccessAsync(
        GetOrganizationAccessHandler handler, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var response = await handler.Handle(new GetOrganizationAccessQuery(), cancellationToken);

        // The panel's one read of what the caller may do (BR-M1-31): derived from the membership at this moment, never cached.
        httpContext.Response.Headers.CacheControl = "no-store";

        return Results.Ok(response);
    }

    private static async Task<IResult> GetMembersAsync(string organizationId, GetMembersHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new GetMembersQuery(organizationId), cancellationToken)).Items);

    private static async Task<IResult> SearchMemberCandidatesAsync(
        string organizationId, string? text, SearchMemberCandidatesHandler handler, CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(new SearchMemberCandidatesQuery(organizationId, text), cancellationToken));

    private static async Task<IResult> AddMemberAsync(
        string organizationId, AddMemberRequest? request, AddMemberHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new AddMemberCommand(organizationId, request?.UserId, request?.Role), cancellationToken);

        return Results.Created($"/api/organizations/{organizationId.ToLowerInvariant()}/members", result.Member);
    }

    private static async Task<IResult> ChangeMemberRoleAsync(
        string organizationId, string userId, RoleRequest? request, ChangeMemberRoleHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new ChangeMemberRoleCommand(organizationId, userId, request?.Role), cancellationToken)).Member);

    private static async Task<IResult> RemoveMemberAsync(
        string organizationId, string userId, RemoveMemberHandler handler, CancellationToken cancellationToken)
    {
        await handler.Handle(new RemoveMemberCommand(organizationId, userId), cancellationToken);

        return Results.NoContent();
    }

    // --- § 2. A team's members ---------------------------------------------------------

    private static async Task<IResult> GetTeamMembersAsync(
        string organizationId, string projectId, string teamId, GetTeamMembersHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new GetTeamMembersQuery(organizationId, projectId, teamId), cancellationToken)).Items);

    private static async Task<IResult> SearchTeamMemberCandidatesAsync(
        string organizationId,
        string projectId,
        string teamId,
        string? text,
        SearchTeamMemberCandidatesHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(new SearchTeamMemberCandidatesQuery(organizationId, projectId, teamId, text), cancellationToken));

    private static async Task<IResult> AddTeamMemberAsync(
        string organizationId,
        string projectId,
        string teamId,
        UserRequest? request,
        AddTeamMemberHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new AddTeamMemberCommand(organizationId, projectId, teamId, request?.UserId), cancellationToken);

        return Results.Created(
            $"/api/organizations/{organizationId.ToLowerInvariant()}/projects/{projectId.ToLowerInvariant()}/teams/{teamId.ToLowerInvariant()}/members",
            result.Member);
    }

    private static async Task<IResult> RemoveTeamMemberAsync(
        string organizationId, string projectId, string teamId, string userId, RemoveTeamMemberHandler handler, CancellationToken cancellationToken)
    {
        await handler.Handle(new RemoveTeamMemberCommand(organizationId, projectId, teamId, userId), cancellationToken);

        return Results.NoContent();
    }

    // --- Request bodies: every field nullable, so a missing one reaches the handler as a validation answer -----

    private sealed record AddMemberRequest(string? UserId, string? Role);

    private sealed record RoleRequest(string? Role);

    private sealed record UserRequest(string? UserId);
}
