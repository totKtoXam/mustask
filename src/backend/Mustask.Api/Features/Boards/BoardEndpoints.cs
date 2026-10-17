using System.Text.Json;
using Mustask.Api.Authorization;
using Mustask.Application.Features.Boards.GetBacklog;
using Mustask.Application.Features.Boards.GetBoard;
using Mustask.Application.Features.Boards.GetProjectBacklog;
using Mustask.Application.Features.Boards.GetProjectBoard;
using Mustask.Application.Features.Boards.ReorderBacklog;
using Mustask.Application.Features.Boards.ReorderProjectBacklog;
using Mustask.Domain.Identity;
using Mustask.Domain.WorkItems;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Mustask.Api.Features.Boards;

/// <summary>
/// M4's routes (mod-006 api-contract § 3), each wired to its own slice. They sit
/// under a team of a project of <c>/api/organizations/{organizationId}/…</c>, each
/// behind the scoped check of its organization code (mod-016 api-contract § 4.2,
/// rows 35–37); beside them, the project's own backlog, reorder and board
/// (feat-039 api-contract § 3), under the project with no team. Route parameters are plain strings with no constraint, so a
/// malformed id or an unknown level reaches the handler and gets its level's
/// not-found code instead of a routing 404. A card move and a creation are
/// mod-003's routes (api-contract § 6).
/// </summary>
public static class BoardEndpoints
{
    private const string Tag = "Boards";

    public static IEndpointRouteBuilder MapBoardEndpoints(this IEndpointRouteBuilder app)
    {
        var team = app.MapGroup("/api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}").WithTags(Tag);

        // § 3.1. The team's backlog of a level. Written with BacklogJson (feat-027): the answer is a tree.
        team.MapGet("/backlogs/{level}", GetBacklogAsync)
            .RequiresOrganizationPermission(PermissionCatalog.WorkItemsWorkItemRead)
            .Produces<BacklogResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // § 3.2. Move one item of the backlog; the panel reads the backlog again.
        team.MapPost("/backlogs/{level}/reorder", ReorderBacklogAsync)
            .RequiresOrganizationPermission(PermissionCatalog.BoardsBacklogReorder)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // § 3.3. The team's board of a level.
        team.MapGet("/boards/{level}", GetBoardAsync)
            .RequiresOrganizationPermission(PermissionCatalog.WorkItemsWorkItemRead)
            .Produces<BoardResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // feat-039: the project's own backlog, reorder and board (api-contract § 3), beside the team's three. A project
        // view has no team in its address.
        var project = app.MapGroup("/api/organizations/{organizationId}/projects/{projectId}").WithTags(Tag);

        // § 3.1. The project's backlog of a level, over every item of the project. Written with BacklogJson: the answer is a tree.
        project.MapGet("/backlogs/{level}", GetProjectBacklogAsync)
            .RequiresOrganizationPermission(PermissionCatalog.WorkItemsWorkItemRead)
            .Produces<ProjectBacklogResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // § 3.2. Move one item of the project's backlog in the project's own order; the panel reads the backlog again.
        project.MapPost("/backlogs/{level}/reorder", ReorderProjectBacklogAsync)
            .RequiresOrganizationPermission(PermissionCatalog.BoardsBacklogReorder)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // § 3.3. The project's board of a level.
        project.MapGet("/boards/{level}", GetProjectBoardAsync)
            .RequiresOrganizationPermission(PermissionCatalog.WorkItemsWorkItemRead)
            .Produces<ProjectBoardResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetBacklogAsync(
        string organizationId,
        string projectId,
        string teamId,
        string level,
        GetBacklogHandler handler,
        IOptions<JsonOptions> json,
        CancellationToken cancellationToken) =>
        TypedResults.Json(
            await handler.Handle(new GetBacklogQuery(organizationId, projectId, teamId, level), cancellationToken),
            BacklogJson.For(json.Value.SerializerOptions));

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

    private static async Task<IResult> GetProjectBacklogAsync(
        string organizationId,
        string projectId,
        string level,
        GetProjectBacklogHandler handler,
        IOptions<JsonOptions> json,
        CancellationToken cancellationToken) =>
        TypedResults.Json(
            await handler.Handle(new GetProjectBacklogQuery(organizationId, projectId, level), cancellationToken),
            BacklogJson.For(json.Value.SerializerOptions));

    private static async Task<IResult> ReorderProjectBacklogAsync(
        string organizationId,
        string projectId,
        string level,
        ReorderBacklogRequest? request,
        ReorderProjectBacklogHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.Handle(
            new ReorderProjectBacklogCommand(organizationId, projectId, level, request?.ItemId, request?.Position, request?.AfterItemId),
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetProjectBoardAsync(
        string organizationId, string projectId, string level, GetProjectBoardHandler handler, CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(new GetProjectBoardQuery(organizationId, projectId, level), cancellationToken));

    // --- Request body: every field nullable, so a missing one reaches the handler as its answer -----

    private sealed record ReorderBacklogRequest(string? ItemId, string? Position, string? AfterItemId);

    /// <summary>
    /// How the backlog's answer is written (feat-027): the platform's own options, with a deeper limit. The tree is as
    /// deep as the longest chain of parents, which the hierarchy bounds at
    /// <see cref="WorkItemHierarchy.MaxChainLength"/> items (BR-T-4, D-9), and every nested row costs the writer two
    /// levels — the row and its <c>children</c> — so the platform's 64 would turn a chain of about thirty links into a
    /// 500 for the whole page. The limit is derived from the bound: the answer object, its <c>items</c> and the first
    /// row take three levels, each further link two, and the deepest value of a row (<c>parent.state.category</c>)
    /// three more; <see cref="Headroom"/> is what the derivation leaves for a nested object a row may yet gain. A chain
    /// stored before the bound that is longer than this fails the write of the answer, as the writer's recursion has
    /// to be bounded; the mapping before it is iterative and cannot fail on any depth. Built once from the first
    /// request's options: they are the application's, the same for every request.
    /// </summary>
    private static class BacklogJson
    {
        private const int AnswerAndFirstRowLevels = 3;
        private const int RowsNestedValueLevels = 3;
        private const int Headroom = 10;

        private const int MaxDepth =
            AnswerAndFirstRowLevels + (2 * (WorkItemHierarchy.MaxChainLength - 1)) + RowsNestedValueLevels + Headroom;

        private static JsonSerializerOptions? options;

        public static JsonSerializerOptions For(JsonSerializerOptions platform) =>
            options ??= new JsonSerializerOptions(platform) { MaxDepth = MaxDepth };
    }
}
