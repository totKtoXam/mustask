using Gridify;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Mustask.Api.Authorization;
using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.Boards.GetBacklogGroupRows;
using Mustask.Application.Features.Boards.GetBoardCells;
using Mustask.Application.Features.Boards.GetViewGroups;
using Mustask.Application.Features.Boards.GetWorkItemGroupRows;
using Mustask.Application.Features.WorkItems.GetOrganizationAssigneeOptions;
using Mustask.Application.Features.WorkItems.GetOrganizationWorkItems;
using Mustask.Domain.Boards;
using Mustask.Domain.Identity;

namespace Mustask.Api.Features.Boards;

/// <summary>
/// The aggregate views' routes (feat-048 api-contract): the grouped counts, the group and cell pages, and the organization-wide
/// work-item list with its assignee options. Each under <c>/api/organizations/{organizationId}</c>, behind the read code of the
/// scoped check; the group and cell reads also behind the read limiter. Values of the query string reach the handler as the
/// strings they are: the handler judges them.
/// </summary>
public static class AggregateViewEndpoints
{
    private const string Tag = "Aggregate views";

    public static IEndpointRouteBuilder MapAggregateViewEndpoints(this IEndpointRouteBuilder app)
    {
        var organization = app.MapGroup("/api/organizations/{organizationId}").WithTags(Tag);

        Map(organization.MapGet("/backlogs/{level}/groups", GetBacklogGroupsAsync), limited: false).Produces<ViewGroupsResponse>();
        Map(organization.MapGet("/boards/{level}/groups", GetBoardGroupsAsync), limited: false).Produces<ViewGroupsResponse>();
        Map(organization.MapGet("/workitems/groups", GetWorkItemGroupsAsync), limited: false).Produces<ViewGroupsResponse>();

        Map(organization.MapGet("/backlogs/{level}/rows", GetBacklogRowsAsync), limited: true).Produces<BacklogGroupRowsResponse>();
        Map(organization.MapGet("/boards/{level}/cells", GetBoardCellsAsync), limited: true).Produces<BoardCellsResponse>();
        Map(organization.MapGet("/workitems/rows", GetWorkItemRowsAsync), limited: true).Produces<WorkItemGroupRowsResponse>();

        Map(organization.MapGet("/workitems", GetOrganizationWorkItemsAsync), limited: false).Produces<Paging<AggregateItemView>>();
        Map(organization.MapGet("/workitems/assignee-options", GetOrganizationAssigneeOptionsAsync), limited: false)
            .Produces<IReadOnlyList<AssigneeFilterOptionView>>();

        return app;
    }

    private static RouteHandlerBuilder Map(RouteHandlerBuilder route, bool limited)
    {
        route
            .RequiresOrganizationPermission(PermissionCatalog.WorkItemsWorkItemRead)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        if (limited)
        {
            route.AddEndpointFilter<AggregateReadLimitFilter>().ProducesProblem(StatusCodes.Status429TooManyRequests);
        }

        return route;
    }

    private static AggregateQuery Of(
        string organizationId,
        string? level,
        string? grouping,
        string[]? departmentId,
        string[]? projectId,
        string[]? teamId,
        string[]? state,
        string[]? assigneeId,
        string[]? search,
        string? group = null,
        string? column = null,
        string? limit = null,
        string? cursor = null) =>
        new(organizationId, level, grouping, departmentId, projectId, teamId, state, assigneeId, search, group, column, limit, cursor);

    private static async Task<IResult> GroupsAsync(
        AggregateView view, AggregateQuery request, GetViewGroupsHandler handler, CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(new GetViewGroupsQuery(view, request), cancellationToken));

    private static Task<IResult> GetBacklogGroupsAsync(
        string organizationId,
        string level,
        string? grouping,
        [FromQuery(Name = "departmentId")] string[]? departmentId,
        [FromQuery(Name = "projectId")] string[]? projectId,
        [FromQuery(Name = "teamId")] string[]? teamId,
        [FromQuery(Name = "state")] string[]? state,
        [FromQuery(Name = "assigneeId")] string[]? assigneeId,
        [FromQuery(Name = "search")] string[]? search,
        GetViewGroupsHandler handler,
        CancellationToken cancellationToken) =>
        GroupsAsync(AggregateView.Backlogs, Of(organizationId, level, grouping, departmentId, projectId, teamId, state, assigneeId, search), handler, cancellationToken);

    private static Task<IResult> GetBoardGroupsAsync(
        string organizationId,
        string level,
        string? grouping,
        [FromQuery(Name = "departmentId")] string[]? departmentId,
        [FromQuery(Name = "projectId")] string[]? projectId,
        [FromQuery(Name = "teamId")] string[]? teamId,
        [FromQuery(Name = "state")] string[]? state,
        [FromQuery(Name = "assigneeId")] string[]? assigneeId,
        [FromQuery(Name = "search")] string[]? search,
        GetViewGroupsHandler handler,
        CancellationToken cancellationToken) =>
        GroupsAsync(AggregateView.Boards, Of(organizationId, level, grouping, departmentId, projectId, teamId, state, assigneeId, search), handler, cancellationToken);

    private static Task<IResult> GetWorkItemGroupsAsync(
        string organizationId,
        string? grouping,
        [FromQuery(Name = "departmentId")] string[]? departmentId,
        [FromQuery(Name = "projectId")] string[]? projectId,
        [FromQuery(Name = "teamId")] string[]? teamId,
        [FromQuery(Name = "state")] string[]? state,
        [FromQuery(Name = "assigneeId")] string[]? assigneeId,
        [FromQuery(Name = "search")] string[]? search,
        GetViewGroupsHandler handler,
        CancellationToken cancellationToken) =>
        GroupsAsync(AggregateView.WorkItems, Of(organizationId, null, grouping, departmentId, projectId, teamId, state, assigneeId, search), handler, cancellationToken);

    private static async Task<IResult> GetBacklogRowsAsync(
        string organizationId,
        string level,
        string? grouping,
        string? group,
        string? limit,
        string? cursor,
        [FromQuery(Name = "departmentId")] string[]? departmentId,
        [FromQuery(Name = "projectId")] string[]? projectId,
        [FromQuery(Name = "teamId")] string[]? teamId,
        [FromQuery(Name = "state")] string[]? state,
        [FromQuery(Name = "assigneeId")] string[]? assigneeId,
        [FromQuery(Name = "search")] string[]? search,
        GetBacklogGroupRowsHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(
            new GetBacklogGroupRowsQuery(Of(organizationId, level, grouping, departmentId, projectId, teamId, state, assigneeId, search, group, null, limit, cursor)),
            cancellationToken));

    private static async Task<IResult> GetBoardCellsAsync(
        string organizationId,
        string level,
        string? grouping,
        string? group,
        string? column,
        string? limit,
        string? cursor,
        [FromQuery(Name = "departmentId")] string[]? departmentId,
        [FromQuery(Name = "projectId")] string[]? projectId,
        [FromQuery(Name = "teamId")] string[]? teamId,
        [FromQuery(Name = "state")] string[]? state,
        [FromQuery(Name = "assigneeId")] string[]? assigneeId,
        [FromQuery(Name = "search")] string[]? search,
        GetBoardCellsHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(
            new GetBoardCellsQuery(Of(organizationId, level, grouping, departmentId, projectId, teamId, state, assigneeId, search, group, column, limit, cursor)),
            cancellationToken));

    private static async Task<IResult> GetWorkItemRowsAsync(
        string organizationId,
        string? grouping,
        string? group,
        string? limit,
        string? cursor,
        [FromQuery(Name = "departmentId")] string[]? departmentId,
        [FromQuery(Name = "projectId")] string[]? projectId,
        [FromQuery(Name = "teamId")] string[]? teamId,
        [FromQuery(Name = "state")] string[]? state,
        [FromQuery(Name = "assigneeId")] string[]? assigneeId,
        [FromQuery(Name = "search")] string[]? search,
        GetWorkItemGroupRowsHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(
            new GetWorkItemGroupRowsQuery(Of(organizationId, null, grouping, departmentId, projectId, teamId, state, assigneeId, search, group, null, limit, cursor)),
            cancellationToken));

    private static async Task<IResult> GetOrganizationWorkItemsAsync(
        string organizationId,
        int? page,
        [FromQuery(Name = "departmentId")] string[]? departmentId,
        [FromQuery(Name = "projectId")] string[]? projectId,
        [FromQuery(Name = "teamId")] string[]? teamId,
        [FromQuery(Name = "state")] string[]? state,
        [FromQuery(Name = "assigneeId")] string[]? assigneeId,
        [FromQuery(Name = "search")] string[]? search,
        GetOrganizationWorkItemsHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(
            new GetOrganizationWorkItemsQuery(Of(organizationId, null, null, departmentId, projectId, teamId, state, assigneeId, search), page),
            cancellationToken));

    private static async Task<IResult> GetOrganizationAssigneeOptionsAsync(
        string organizationId,
        [FromQuery(Name = "departmentId")] string[]? departmentId,
        [FromQuery(Name = "projectId")] string[]? projectId,
        [FromQuery(Name = "teamId")] string[]? teamId,
        [FromQuery(Name = "state")] string[]? state,
        GetOrganizationAssigneeOptionsHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(
            new GetOrganizationAssigneeOptionsQuery(Of(organizationId, null, null, departmentId, projectId, teamId, state, null, null)),
            cancellationToken));
}
