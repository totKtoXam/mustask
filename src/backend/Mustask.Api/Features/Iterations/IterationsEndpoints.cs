using Mustask.Api.Authorization;
using Mustask.Application.Abstractions.Iterations;
using Mustask.Application.Features.Iterations;
using Mustask.Application.Features.Iterations.CloseSprint;
using Mustask.Application.Features.Iterations.CreateSprint;
using Mustask.Application.Features.Iterations.GetCloseSprintPreview;
using Mustask.Application.Features.Iterations.GetSprint;
using Mustask.Application.Features.Iterations.GetSprintDynamics;
using Mustask.Application.Features.Iterations.GetSprintOutcomeItems;
using Mustask.Application.Features.Iterations.GetSprintParticipants;
using Mustask.Application.Features.Iterations.GetSprints;
using Mustask.Application.Features.Iterations.RemoveSprint;
using Mustask.Application.Features.Iterations.StartSprint;
using Mustask.Application.Features.Iterations.UpdateSprint;
using Mustask.Domain.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Mustask.Api.Features.Iterations;

/// <summary>
/// M5's routes (mod-007 api-contract § 2), each wired to its own slice, under the team of
/// <c>/api/organizations/{organizationId}/teams/{teamId}/sprints</c>. Reads need the team's read right, which every organization
/// role holds; writes need <c>iterations.sprint.manage</c> on the team (ADR-0029, ADR-0038). Route parameters are plain strings
/// with no constraint, so a malformed id reaches the handler and gets its level's not-found code instead of a routing 404.
/// </summary>
public static class IterationsEndpoints
{
    private const string Tag = "Iterations";

    public static IEndpointRouteBuilder MapIterationsEndpoints(this IEndpointRouteBuilder app)
    {
        var sprints = app.MapGroup("/api/organizations/{organizationId}/teams/{teamId}/sprints").WithTags(Tag);

        sprints.MapGet("", GetSprintsAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsTeamRead)
            .Produces<SprintListPage>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        sprints.MapPost("", CreateSprintAsync)
            .RequiresOrganizationPermission(PermissionCatalog.IterationsSprintManage, ResourceScope.Team)
            .Produces<SprintResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        sprints.MapPut("/{sprintId}", UpdateSprintAsync)
            .RequiresOrganizationPermission(PermissionCatalog.IterationsSprintManage, ResourceScope.Team)
            .Produces<SprintResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        sprints.MapPost("/{sprintId}/start", StartSprintAsync)
            .RequiresOrganizationPermission(PermissionCatalog.IterationsSprintManage, ResourceScope.Team)
            .Produces<SprintResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        sprints.MapDelete("/{sprintId}", RemoveSprintAsync)
            .RequiresOrganizationPermission(PermissionCatalog.IterationsSprintManage, ResourceScope.Team)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        sprints.MapGet("/{sprintId}/close-preview", GetCloseSprintPreviewAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsTeamRead)
            .Produces<ClosePreview>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        sprints.MapPost("/{sprintId}/close", CloseSprintAsync)
            .RequiresOrganizationPermission(PermissionCatalog.IterationsSprintManage, ResourceScope.Team)
            .Produces<SprintResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        sprints.MapGet("/{sprintId}", GetSprintAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsTeamRead)
            .Produces<GetSprintResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        sprints.MapGet("/{sprintId}/dynamics", GetSprintDynamicsAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsTeamRead)
            .Produces<GetSprintDynamicsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        sprints.MapGet("/{sprintId}/participants", GetSprintParticipantsAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsTeamRead)
            .Produces<GetSprintParticipantsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        sprints.MapGet("/{sprintId}/items", GetSprintOutcomeItemsAsync)
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsTeamRead)
            .Produces<GetSprintOutcomeItemsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetSprintsAsync(
        string organizationId,
        string teamId,
        [FromQuery] string? before,
        [FromQuery] bool? open,
        GetSprintsHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(new GetSprintsQuery(organizationId, teamId, before, open), cancellationToken));

    private static async Task<IResult> CreateSprintAsync(
        string organizationId,
        string teamId,
        SprintRequest? request,
        CreateSprintHandler handler,
        CancellationToken cancellationToken)
    {
        var created = await handler.Handle(
            new CreateSprintCommand(organizationId, teamId, request?.Name, request?.StartDate, request?.EndDate), cancellationToken);

        return Results.Created($"/api/organizations/{organizationId}/teams/{teamId}/sprints/{created.Id}", created);
    }

    private static async Task<IResult> UpdateSprintAsync(
        string organizationId,
        string teamId,
        string sprintId,
        SprintRequest? request,
        UpdateSprintHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(
            new UpdateSprintCommand(organizationId, teamId, sprintId, request?.Name, request?.StartDate, request?.EndDate),
            cancellationToken));

    private static async Task<IResult> StartSprintAsync(
        string organizationId, string teamId, string sprintId, StartSprintHandler handler, CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(new StartSprintCommand(organizationId, teamId, sprintId), cancellationToken));

    private static async Task<IResult> RemoveSprintAsync(
        string organizationId, string teamId, string sprintId, RemoveSprintHandler handler, CancellationToken cancellationToken)
    {
        await handler.Handle(new RemoveSprintCommand(organizationId, teamId, sprintId), cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetCloseSprintPreviewAsync(
        string organizationId, string teamId, string sprintId, GetCloseSprintPreviewHandler handler, CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(new GetCloseSprintPreviewQuery(organizationId, teamId, sprintId), cancellationToken));

    private static async Task<IResult> CloseSprintAsync(
        string organizationId,
        string teamId,
        string sprintId,
        CloseSprintRequest? request,
        CloseSprintHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(
            new CloseSprintCommand(organizationId, teamId, sprintId, request?.DestinationSprintId), cancellationToken));

    private static async Task<IResult> GetSprintAsync(
        string organizationId, string teamId, string sprintId, GetSprintHandler handler, CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(new GetSprintQuery(organizationId, teamId, sprintId), cancellationToken));

    private static async Task<IResult> GetSprintDynamicsAsync(
        string organizationId,
        string teamId,
        string sprintId,
        [FromQuery] string? level,
        [FromQuery] string? from,
        [FromQuery] string? to,
        GetSprintDynamicsHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(
            new GetSprintDynamicsQuery(organizationId, teamId, sprintId, level, from, to), cancellationToken));

    private static async Task<IResult> GetSprintParticipantsAsync(
        string organizationId,
        string teamId,
        string sprintId,
        [FromQuery] string? level,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? offset,
        GetSprintParticipantsHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(
            new GetSprintParticipantsQuery(organizationId, teamId, sprintId, level, from, to, offset), cancellationToken));

    private static async Task<IResult> GetSprintOutcomeItemsAsync(
        string organizationId,
        string teamId,
        string sprintId,
        [FromQuery] string? offset,
        GetSprintOutcomeItemsHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(new GetSprintOutcomeItemsQuery(organizationId, teamId, sprintId, offset), cancellationToken));

    // --- Request bodies: every field nullable, so a missing one reaches the handler as its answer -----

    private sealed record SprintRequest(string? Name, DateOnly? StartDate, DateOnly? EndDate);

    private sealed record CloseSprintRequest(string? DestinationSprintId);
}
