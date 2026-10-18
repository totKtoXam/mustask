using Mustask.Api.Authorization;
using Mustask.Application.Features.Organizations.AddTeam;
using Mustask.Application.Features.Organizations.CreateArea;
using Mustask.Application.Features.Organizations.CreateOrganization;
using Mustask.Application.Features.Organizations.CreateProject;
using Mustask.Application.Features.Organizations.DeactivateOrganization;
using Mustask.Application.Features.Organizations.DeactivateProject;
using Mustask.Application.Features.Organizations.DeleteArea;
using Mustask.Application.Features.Organizations.DeleteTeam;
using Mustask.Application.Features.Organizations.GetAreaTree;
using Mustask.Application.Features.Organizations.GetDeactivatedOrganizations;
using Mustask.Application.Features.Organizations.GetDeactivatedProjects;
using Mustask.Application.Features.Organizations.GetOrganization;
using Mustask.Application.Features.Organizations.GetOrganizationTeams;
using Mustask.Application.Features.Organizations.GetOrganizations;
using Mustask.Application.Features.Organizations.GetProject;
using Mustask.Application.Features.Organizations.GetProjects;
using Mustask.Application.Features.Organizations.GetTeam;
using Mustask.Application.Features.Organizations.GetTeamDeletionCounts;
using Mustask.Application.Features.Organizations.GetTeamOwnedItemCount;
using Mustask.Application.Features.Organizations.GetTeamProjects;
using Mustask.Application.Features.Organizations.GetTeams;
using Mustask.Application.Features.Organizations.LinkProjectsToTeam;
using Mustask.Application.Features.Organizations.LinkTeamsToProject;
using Mustask.Application.Features.Organizations.MoveArea;
using Mustask.Application.Features.Organizations.RenameArea;
using Mustask.Application.Features.Organizations.RenameOrganization;
using Mustask.Application.Features.Organizations.RenameProject;
using Mustask.Application.Features.Organizations.RenameTeam;
using Mustask.Application.Features.Organizations.RestoreOrganization;
using Mustask.Application.Features.Organizations.RestoreProject;
using Mustask.Application.Features.Organizations.SetDefaultTeam;
using Mustask.Application.Features.Organizations.SetProjectDescription;
using Mustask.Application.Features.Organizations.SetTeamDescription;
using Mustask.Application.Features.Organizations.UnlinkTeam;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Mustask.Api.Features.Organizations;

/// <summary>
/// <c>/api/organizations/*</c>: M1's structure (mod-002 api-contract § 1-§ 5),
/// every route mapped here and wired to its own slice. Every route with an
/// <c>{organizationId}</c> declares its organization code with
/// <c>RequiresOrganizationPermission</c> (mod-016 ADR-0027, api-contract § 4.1):
/// the scoped check answers 401, then 404 for a non-member, then 403 for a role
/// without the code, all before a body is read. The route parameters are plain
/// strings with no constraint, so a malformed id reaches the check or the
/// handler and gets its level's not-found code instead of a routing 404
/// (BR-M1-20). Id fields in bodies are strings for the same reason.
/// </summary>
public static class OrganizationEndpoints
{
    private const string Tag = "Organizations";

    private const string ListsOnlyMine =
        "Lists only the organizations the caller belongs to, in any role (mod-016 BR-M1-7, api-contract § 4.1 row 1): "
        + "the route names no organization, so no organization code applies, and a caller with no membership gets an empty list.";

    private const string DeactivatedListsOnlyMine =
        "Lists only the deactivated organizations where the caller's role may deactivate and restore the organization "
        + "(mod-016 BR-M1-35, api-contract § 4.1 row 2): the route names no organization, so the narrowing is by membership "
        + "inside the read, and the roles are derived from the bundles.";

    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var organizations = app.MapGroup("/api/organizations").WithTags(Tag);

        // § 1. Organizations. "/deactivated" is a literal segment: the router ranks it above "{organizationId}".
        organizations.MapGet("/", GetOrganizationsAsync)
            .AuthenticatedOnly(ListsOnlyMine)
            .Produces<IReadOnlyList<Application.Features.Organizations.GetOrganizations.OrganizationListItemResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        organizations.MapGet("/deactivated", GetDeactivatedOrganizationsAsync)
            .AuthenticatedOnly(DeactivatedListsOnlyMine)
            .Produces<IReadOnlyList<Application.Features.Organizations.GetDeactivatedOrganizations.OrganizationListItemResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        organizations.MapPost("/", CreateOrganizationAsync)
            .RequiresPermission(PermissionCatalog.OrganizationsOrganizationCreate)
            .WithForbiddenErrorCode(OrganizationErrorCodes.OrganizationCreateForbidden)
            .Produces<Application.Features.Organizations.CreateOrganization.OrganizationResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        organizations.MapGet("/{organizationId}", GetOrganizationAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsOrganizationRead)
            .Produces<Application.Features.Organizations.GetOrganization.OrganizationResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        organizations.MapPut("/{organizationId}/name", RenameOrganizationAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsOrganizationWrite)
            .Produces<Application.Features.Organizations.RenameOrganization.OrganizationResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        organizations.MapPost("/{organizationId}/deactivate", DeactivateOrganizationAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsOrganizationDeactivate)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // The one request a deactivated organization accepts (api-contract § 1.7).
        organizations.MapPost("/{organizationId}/restore", RestoreOrganizationAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsOrganizationDeactivate, acceptsDeactivated: true)
            .Produces<Application.Features.Organizations.RestoreOrganization.OrganizationResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // § 2. Projects.
        var projects = organizations.MapGroup("/{organizationId}/projects");

        projects.MapGet("/", GetProjectsAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsProjectRead)
            .Produces<IReadOnlyList<Application.Features.Organizations.GetProjects.ProjectListItemResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        projects.MapGet("/deactivated", GetDeactivatedProjectsAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsProjectDeactivate)
            .Produces<IReadOnlyList<Application.Features.Organizations.GetDeactivatedProjects.ProjectListItemResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        projects.MapPost("/", CreateProjectAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsProjectWrite)
            .Produces<Application.Features.Organizations.CreateProject.ProjectResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        projects.MapGet("/{projectId}", GetProjectAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsProjectRead)
            .Produces<Application.Features.Organizations.GetProject.ProjectResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        projects.MapPut("/{projectId}/name", RenameProjectAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsProjectWrite)
            .Produces<Application.Features.Organizations.RenameProject.ProjectResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        projects.MapPost("/{projectId}/deactivate", DeactivateProjectAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsProjectDeactivate)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        projects.MapPost("/{projectId}/restore", RestoreProjectAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsProjectDeactivate)
            .Produces<Application.Features.Organizations.RestoreProject.ProjectResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // feat-028 api-contract § 2.4. Name the project's default team, or clear the setting (an empty body, {} and
        // { "teamId": null } all clear it); the team must be linked to the project. Declared project.write (mod-017 had team.write).
        projects.MapPut("/{projectId}/default-team", SetDefaultTeamAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsProjectWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // feat-028 api-contract § 2.4. A project's description, kept as typed; the empty string and null clear it.
        projects.MapPut("/{projectId}/description", SetProjectDescriptionAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsProjectWrite)
            .Produces<Application.Features.Organizations.SetProjectDescription.ProjectResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // § 3a. The project's end of its teams (feat-028 api-contract § 2.4): the linked teams, the link, the unlink.
        var projectTeams = projects.MapGroup("/{projectId}/teams");

        projectTeams.MapGet("/", GetTeamsAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsTeamRead)
            .Produces<IReadOnlyList<Application.Features.Organizations.GetTeams.TeamResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // The old "add a team" path with another meaning: link teams to the project. The second code, organizations.team.link,
        // is asked by the handler first of all (D-7).
        projectTeams.MapPost("/", LinkTeamsToProjectAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsProjectLink)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // SC-09. Take a team off the project, giving its items there to a destination (a POST: a body on a DELETE is dropped by
        // some proxies, D-6).
        projectTeams.MapPost("/{teamId}/unlink", UnlinkTeamAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsProjectLink)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // What an unlink would reassign: the items the team owns in this project, in any state.
        projectTeams.MapGet("/{teamId}/unlink-count", GetTeamOwnedItemCountAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsProjectLink)
            .Produces<GetTeamOwnedItemCountResult>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // § 3b. The organization's teams (feat-028 api-contract § 2.1, § 2.2). The team's members are mapped in MembershipEndpoints.
        var teams = organizations.MapGroup("/{organizationId}/teams");

        teams.MapGet("/", GetOrganizationTeamsAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsTeamRead)
            .Produces<IReadOnlyList<TeamListItemResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // The second codes of a creation that links or staffs the team are asked by the handler first of all (D-7).
        teams.MapPost("/", AddTeamAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsTeamCreate)
            .Produces<Application.Features.Organizations.AddTeam.TeamResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        teams.MapGet("/{teamId}", GetTeamAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsTeamRead)
            .Produces<Application.Features.Organizations.GetTeam.TeamResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        teams.MapPut("/{teamId}/name", RenameTeamAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsTeamWrite)
            .Produces<Application.Features.Organizations.RenameTeam.TeamResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        teams.MapPut("/{teamId}/description", SetTeamDescriptionAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsTeamWrite)
            .Produces<Application.Features.Organizations.SetTeamDescription.TeamResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // What a deletion would reassign, per project the team is linked to.
        teams.MapGet("/{teamId}/deletion-counts", GetTeamDeletionCountsAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsTeamDelete)
            .Produces<GetTeamDeletionCountsResult>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        teams.MapDelete("/{teamId}", DeleteTeamAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsTeamDelete)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        teams.MapGet("/{teamId}/projects", GetTeamProjectsAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsTeamRead)
            .Produces<IReadOnlyList<Application.Features.Organizations.GetTeamProjects.TeamProjectResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // The team's end of a link; the second code, organizations.project.link, is asked by the handler first of all (D-7).
        teams.MapPost("/{teamId}/projects", LinkProjectsToTeamAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsTeamLink)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // § 4. Areas.
        var areas = projects.MapGroup("/{projectId}/areas");

        areas.MapGet("/", GetAreaTreeAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsAreaRead)
            .Produces<IReadOnlyList<AreaNodeResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        areas.MapPost("/", CreateAreaAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsAreaWrite)
            .Produces<Application.Features.Organizations.CreateArea.AreaResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        areas.MapPut("/{areaId}/name", RenameAreaAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsAreaWrite)
            .Produces<Application.Features.Organizations.RenameArea.AreaResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        areas.MapPut("/{areaId}/parent", MoveAreaAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsAreaWrite)
            .Produces<Application.Features.Organizations.MoveArea.AreaResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        areas.MapDelete("/{areaId}", DeleteAreaAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsAreaWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    // --- § 1. Organizations ---------------------------------------------------------

    private static async Task<IResult> GetOrganizationsAsync(GetOrganizationsHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new GetOrganizationsQuery(), cancellationToken)).Items);

    private static async Task<IResult> GetDeactivatedOrganizationsAsync(GetDeactivatedOrganizationsHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new GetDeactivatedOrganizationsQuery(), cancellationToken)).Items);

    private static async Task<IResult> CreateOrganizationAsync(NameRequest? request, CreateOrganizationHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new CreateOrganizationCommand(request?.Name), cancellationToken);

        return Results.Created($"/api/organizations/{result.Response.Id}", result.Response);
    }

    private static async Task<IResult> GetOrganizationAsync(string organizationId, GetOrganizationHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new GetOrganizationQuery(organizationId), cancellationToken)).Response);

    private static async Task<IResult> RenameOrganizationAsync(
        string organizationId, NameRequest? request, RenameOrganizationHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new RenameOrganizationCommand(organizationId, request?.Name), cancellationToken)).Response);

    private static async Task<IResult> DeactivateOrganizationAsync(string organizationId, DeactivateOrganizationHandler handler, CancellationToken cancellationToken)
    {
        await handler.Handle(new DeactivateOrganizationCommand(organizationId), cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> RestoreOrganizationAsync(string organizationId, RestoreOrganizationHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new RestoreOrganizationCommand(organizationId), cancellationToken)).Response);

    // --- § 2. Projects ----------------------------------------------------------------

    private static async Task<IResult> GetProjectsAsync(string organizationId, GetProjectsHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new GetProjectsQuery(organizationId), cancellationToken)).Items);

    private static async Task<IResult> GetDeactivatedProjectsAsync(string organizationId, GetDeactivatedProjectsHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new GetDeactivatedProjectsQuery(organizationId), cancellationToken)).Items);

    private static async Task<IResult> CreateProjectAsync(
        string organizationId, CreateProjectRequest? request, CreateProjectHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new CreateProjectCommand(organizationId, request?.Name, request?.Key, request?.Description, request?.TeamIds),
            cancellationToken);

        return Results.Created($"/api/organizations/{result.Response.OrganizationId}/projects/{result.Response.Id}", result.Response);
    }

    private static async Task<IResult> SetProjectDescriptionAsync(
        string organizationId, string projectId, DescriptionRequest? request, SetProjectDescriptionHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new SetProjectDescriptionCommand(organizationId, projectId, request?.Description), cancellationToken)).Response);

    private static async Task<IResult> GetProjectAsync(string organizationId, string projectId, GetProjectHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new GetProjectQuery(organizationId, projectId), cancellationToken)).Response);

    private static async Task<IResult> RenameProjectAsync(
        string organizationId, string projectId, NameRequest? request, RenameProjectHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new RenameProjectCommand(organizationId, projectId, request?.Name), cancellationToken)).Response);

    private static async Task<IResult> DeactivateProjectAsync(
        string organizationId, string projectId, DeactivateProjectHandler handler, CancellationToken cancellationToken)
    {
        await handler.Handle(new DeactivateProjectCommand(organizationId, projectId), cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> RestoreProjectAsync(
        string organizationId, string projectId, RestoreProjectHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new RestoreProjectCommand(organizationId, projectId), cancellationToken)).Response);

    // --- § 3. Teams -------------------------------------------------------------------

    private static async Task<IResult> SetDefaultTeamAsync(
        string organizationId, string projectId, DefaultTeamRequest? request, SetDefaultTeamHandler handler, CancellationToken cancellationToken)
    {
        // A missing body is {}: clear (api-contract § 2.8, D-7).
        await handler.Handle(new SetDefaultTeamCommand(organizationId, projectId, request?.TeamId), cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetTeamOwnedItemCountAsync(
        string organizationId, string projectId, string teamId, GetTeamOwnedItemCountHandler handler, CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(new GetTeamOwnedItemCountQuery(organizationId, projectId, teamId), cancellationToken));

    private static async Task<IResult> GetTeamsAsync(string organizationId, string projectId, GetTeamsHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new GetTeamsQuery(organizationId, projectId), cancellationToken)).Items);

    private static async Task<IResult> LinkTeamsToProjectAsync(
        string organizationId, string projectId, LinkTeamsRequest? request, LinkTeamsToProjectHandler handler, CancellationToken cancellationToken)
    {
        // An absent body, an old { "name" } body and an empty list all read as an empty list: a no-op 204 (D-14).
        await handler.Handle(new LinkTeamsToProjectCommand(organizationId, projectId, request?.TeamIds), cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> UnlinkTeamAsync(
        string organizationId, string projectId, string teamId, UnlinkRequest? request, UnlinkTeamHandler handler, CancellationToken cancellationToken)
    {
        await handler.Handle(new UnlinkTeamCommand(organizationId, projectId, teamId, ReadDestination(request?.Destination)), cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetOrganizationTeamsAsync(string organizationId, GetOrganizationTeamsHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new GetOrganizationTeamsQuery(organizationId), cancellationToken)).Items);

    private static async Task<IResult> GetTeamAsync(
        string organizationId, string teamId, GetTeamHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new GetTeamQuery(organizationId, teamId), cancellationToken)).Response);

    private static async Task<IResult> AddTeamAsync(
        string organizationId, AddTeamRequest? request, AddTeamHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new AddTeamCommand(organizationId, request?.Name, request?.Description, request?.MemberIds, request?.ProjectIds),
            cancellationToken);

        return Results.Created($"/api/organizations/{organizationId.ToLowerInvariant()}/teams/{result.Response.Id}", result.Response);
    }

    private static async Task<IResult> RenameTeamAsync(
        string organizationId, string teamId, NameRequest? request, RenameTeamHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new RenameTeamCommand(organizationId, teamId, request?.Name), cancellationToken)).Response);

    private static async Task<IResult> SetTeamDescriptionAsync(
        string organizationId, string teamId, DescriptionRequest? request, SetTeamDescriptionHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new SetTeamDescriptionCommand(organizationId, teamId, request?.Description), cancellationToken)).Response);

    private static async Task<IResult> GetTeamDeletionCountsAsync(
        string organizationId, string teamId, GetTeamDeletionCountsHandler handler, CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(new GetTeamDeletionCountsQuery(organizationId, teamId), cancellationToken));

    private static async Task<IResult> DeleteTeamAsync(
        string organizationId, string teamId, DeleteTeamHandler handler, CancellationToken cancellationToken)
    {
        await handler.Handle(new DeleteTeamCommand(organizationId, teamId), cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetTeamProjectsAsync(
        string organizationId, string teamId, GetTeamProjectsHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new GetTeamProjectsQuery(organizationId, teamId), cancellationToken)).Items);

    private static async Task<IResult> LinkProjectsToTeamAsync(
        string organizationId, string teamId, LinkProjectsRequest? request, LinkProjectsToTeamHandler handler, CancellationToken cancellationToken)
    {
        await handler.Handle(new LinkProjectsToTeamCommand(organizationId, teamId, request?.ProjectIds), cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// The unlink's <c>destination</c> wrapper (D-8): <c>{ "teamId": "…" }</c> names a team and <c>{ "teamId": null }</c> is «Без команды».
    /// The wrapper absent, <c>null</c> or <c>{}</c> — its <c>teamId</c> key missing — is <c>null</c> here, which the handler answers
    /// with 400 <c>destination_required</c>: «Без команды» is never the answer to a missing key, so a client defect cannot move every
    /// item to no team silently. A <c>destination</c> that is not an object, or a <c>teamId</c> that is neither a string nor <c>null</c>,
    /// is a malformed body (400 <c>http.bad_request</c>), as a number in any other id field is.
    /// </summary>
    private static UnlinkDestination? ReadDestination(JsonElement? destination)
    {
        if (destination is not { } wrapper || wrapper.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (wrapper.ValueKind != JsonValueKind.Object)
        {
            throw new BadHttpRequestException("The destination of an unlink is an object with a teamId.");
        }

        foreach (var property in wrapper.EnumerateObject())
        {
            // Property names bind case-insensitively like every other body of the API.
            if (!string.Equals(property.Name, "teamId", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return property.Value.ValueKind switch
            {
                JsonValueKind.String => new UnlinkDestination(property.Value.GetString()),
                JsonValueKind.Null => new UnlinkDestination(null),
                _ => throw new BadHttpRequestException("The teamId of an unlink's destination is a string or null."),
            };
        }

        return null;
    }

    // --- § 4. Areas -------------------------------------------------------------------

    private static async Task<IResult> GetAreaTreeAsync(string organizationId, string projectId, GetAreaTreeHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new GetAreaTreeQuery(organizationId, projectId), cancellationToken)).Items);

    private static async Task<IResult> CreateAreaAsync(
        string organizationId, string projectId, CreateAreaRequest? request, CreateAreaHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new CreateAreaCommand(organizationId, projectId, request?.ParentId, request?.Name),
            cancellationToken);

        return Results.Created($"{ProjectPath(organizationId, result.Response.ProjectId)}/areas/{result.Response.Id}", result.Response);
    }

    private static async Task<IResult> RenameAreaAsync(
        string organizationId, string projectId, string areaId, NameRequest? request, RenameAreaHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new RenameAreaCommand(organizationId, projectId, areaId, request?.Name), cancellationToken)).Response);

    private static async Task<IResult> MoveAreaAsync(
        string organizationId, string projectId, string areaId, ParentRequest? request, MoveAreaHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new MoveAreaCommand(organizationId, projectId, areaId, request?.ParentId), cancellationToken)).Response);

    private static async Task<IResult> DeleteAreaAsync(
        string organizationId, string projectId, string areaId, DeleteAreaHandler handler, CancellationToken cancellationToken)
    {
        await handler.Handle(new DeleteAreaCommand(organizationId, projectId, areaId), cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// The canonical address of a project for a <c>Location</c> header. The
    /// organization id passed the filter, so it is the canonical form in some
    /// letter case; lower-casing it gives the form responses write (api-contract § 0.2).
    /// </summary>
    private static string ProjectPath(string organizationId, Guid projectId) =>
        $"/api/organizations/{organizationId.ToLowerInvariant()}/projects/{projectId}";

    // --- Request bodies: every field nullable, so a missing one reaches the handler as a validation answer -----

    private sealed record NameRequest(string? Name);

    private sealed record CreateProjectRequest(string? Name, string? Key, string? Description, IReadOnlyList<string?>? TeamIds);

    /// <summary>feat-028 api-contract § 2.1: a team with its optional description, people and projects.</summary>
    private sealed record AddTeamRequest(string? Name, string? Description, IReadOnlyList<string?>? MemberIds, IReadOnlyList<string?>? ProjectIds);

    /// <summary>feat-028 api-contract § 2.1, § 2.4: <c>description</c> a string, or <c>null</c> (or absent) to clear it.</summary>
    private sealed record DescriptionRequest(string? Description);

    private sealed record LinkTeamsRequest(IReadOnlyList<string?>? TeamIds);

    private sealed record LinkProjectsRequest(IReadOnlyList<string?>? ProjectIds);

    /// <summary>
    /// feat-028 api-contract § 2.4: the unlink's body. <c>destination</c> is bound as a raw JSON value (D-8) because its
    /// <c>teamId</c> key must be told apart from a missing one: a typed record would read both as <c>null</c>.
    /// </summary>
    private sealed record UnlinkRequest(JsonElement? Destination);

    /// <summary>mod-017 api-contract § 2.8: <c>teamId</c> a string, or <c>null</c> (or absent) to clear the setting.</summary>
    private sealed record DefaultTeamRequest(string? TeamId);

    private sealed record CreateAreaRequest(string? ParentId, string? Name);

    private sealed record ParentRequest(string? ParentId);
}
