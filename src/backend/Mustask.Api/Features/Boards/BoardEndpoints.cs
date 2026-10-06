using Mustask.Api.Authorization;
using Mustask.Application.Features.Boards.GetBacklog;
using Mustask.Application.Features.Boards.GetBoard;
using Mustask.Application.Features.Boards.ReorderBacklog;
using Mustask.Domain.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Mustask.Api.Features.Boards;

/// <summary>
/// M4's routes (mod-006 api-contract § 3), each wired to its own slice. They sit
/// under a team of a project of <c>/api/organizations/{organizationId}/…</c>,
/// behind the authenticated guard and mod-002's organization-context filter
/// (BR-M4-27). Route parameters are plain strings with no constraint, so a
/// malformed id or an unknown level reaches the handler and gets its level's
/// not-found code instead of a routing 404. A card move and a creation are
/// mod-003's routes (api-contract § 6).
/// </summary>
public static class BoardEndpoints
{
    private const string Tag = "Boards";

    private static readonly string ReadAccess =
        $"Declares {OrganizationPermissionCatalog.WorkItemsWorkItemRead}, an organization-scoped code that no token carries "
        + "and nothing checks yet (BR-M4-27): any signed-in user, until mod-016 retrofits the check; a non-member then "
        + "gets the not-found answer, not a 403.";

    private static readonly string ReorderAccess =
        $"Declares {OrganizationPermissionCatalog.BoardsBacklogReorder}, an organization-scoped code that no token carries "
        + "and nothing checks yet (BR-M4-27): any signed-in user, until mod-016 retrofits the check; a non-member then "
        + "gets the not-found answer, not a 403.";

    public static IEndpointRouteBuilder MapBoardEndpoints(this IEndpointRouteBuilder app)
    {
        var team = app.MapGroup("/api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}").WithTags(Tag);

        // § 3.1. The team's backlog of a level.
        team.MapGet("/backlogs/{level}", GetBacklogAsync)
            .AuthenticatedOnly(ReadAccess)
            .RequiresOrganizationContext()
            .Produces<BacklogResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // § 3.2. Move one item of the backlog; the panel reads the backlog again.
        team.MapPost("/backlogs/{level}/reorder", ReorderBacklogAsync)
            .AuthenticatedOnly(ReorderAccess)
            .RequiresOrganizationContext()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // § 3.3. The team's board of a level.
        team.MapGet("/boards/{level}", GetBoardAsync)
            .AuthenticatedOnly(ReadAccess)
            .RequiresOrganizationContext()
            .Produces<BoardResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetBacklogAsync(
        string organizationId, string projectId, string teamId, string level, GetBacklogHandler handler, CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(new GetBacklogQuery(organizationId, projectId, teamId, level), cancellationToken));

    private static async Task<IResult> ReorderBacklogAsync(
        string organizationId,
        string projectId,
        string teamId,
        string level,
        ReorderBacklogRequest? request,
        ReorderBacklogHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.Handle(
            new ReorderBacklogCommand(organizationId, projectId, teamId, level, request?.ItemId, request?.Position, request?.AfterItemId),
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetBoardAsync(
        string organizationId, string projectId, string teamId, string level, GetBoardHandler handler, CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(new GetBoardQuery(organizationId, projectId, teamId, level), cancellationToken));

    // --- Request body: every field nullable, so a missing one reaches the handler as its answer -----

    private sealed record ReorderBacklogRequest(string? ItemId, string? Position, string? AfterItemId);
}
