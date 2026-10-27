using Mustask.Api.Authorization;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.WorkItems.CreateWorkItem;
using Mustask.Application.Features.WorkItems.DeleteWorkItem;
using Mustask.Application.Features.WorkItems.GetAssigneeCandidates;
using Mustask.Application.Features.WorkItems.GetAssigneeOptions;
using Mustask.Application.Features.WorkItems.GetParentCandidates;
using Mustask.Application.Features.WorkItems.GetWorkItem;
using Mustask.Application.Features.WorkItems.GetWorkItems;
using Mustask.Application.Features.WorkItems.PurgeWorkItem;
using Mustask.Application.Features.WorkItems.RestoreWorkItem;
using Mustask.Application.Features.WorkItems.SaveWorkItem;
using Mustask.Application.Features.WorkItems.SetWorkItemParent;
using Mustask.Domain.Identity;
using Mustask.Domain.WorkItems;
using Gridify;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Mustask.Api.Features.WorkItems;

/// <summary>
/// M3's work item routes (mod-003 api-contract § 2), each wired to its own
/// slice. They sit under <c>/api/organizations/{organizationId}/…</c>, as
/// mod-002's and mod-001's do, each behind the scoped check of its organization
/// code (mod-016 api-contract § 4.2, rows 28–34): the code comes from the caller's
/// membership, never from the token. Route parameters are plain strings with no
/// constraint, so a malformed id or identifier reaches the handler and gets its
/// level's not-found code instead of a routing 404.
/// </summary>
public static class WorkItemEndpoints
{
    private const string Tag = "WorkItems";

    public static IEndpointRouteBuilder MapWorkItemEndpoints(this IEndpointRouteBuilder app)
    {
        var workItems = app.MapGroup("/api/organizations/{organizationId}/projects/{projectId}/workitems").WithTags(Tag);

        // § 2.1. The project's list.
        workItems.MapGet("/", GetWorkItemsAsync)
            .RequiresOrganizationPermission(PermissionCatalog.WorkItemsWorkItemRead)
            .Produces<Paging<WorkItemListItemView>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // feat-003 § 2.2. The options of the list's «Исполнитель» filter: the people assigned to the project's items. A read
        // inside one project, so the read code alone (ADR-0029 § 7); not the card's picker (`assignee-candidates`, below).
        workItems.MapGet("/assignee-options", GetAssigneeOptionsAsync)
            .RequiresOrganizationPermission(PermissionCatalog.WorkItemsWorkItemRead)
            .Produces<IReadOnlyList<AssigneeFilterOptionView>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // § 2.2. Create; a replay of a submission answers 200 with the item it created. With feat-027's `parentId`
        // it creates the item beneath that parent, refused by the parent write's 404 and 409s. The route's one code
        // is workitems.workitem.write; a `parentId` is a parent write, which the handler also holds to
        // workitems.link.write, first of all its answers (api-contract § 4.2, "The second code of rows 29 and 31").
        workItems.MapPost("/", CreateWorkItemAsync)
            .RequiresOrganizationPermission(PermissionCatalog.WorkItemsWorkItemWrite)
            .Produces<WorkItemView>(StatusCodes.Status201Created)
            .Produces<WorkItemView>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // § 2.3. One item by its identifier.
        workItems.MapGet("/{identifier}", GetWorkItemAsync)
            .RequiresOrganizationPermission(PermissionCatalog.WorkItemsWorkItemRead)
            .Produces<WorkItemView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // § 2.4. Save the form: exactly the fields sent; feat-001 § 3.3 adds the parent, which the handler also holds
        // to workitems.link.write, first of all its answers (feat-001 BR-H-18).
        workItems.MapPatch("/{identifier}", SaveWorkItemAsync)
            .RequiresOrganizationPermission(PermissionCatalog.WorkItemsWorkItemWrite)
            .Produces<WorkItemView>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // feat-001 § 3.2. The parent write: set, replace or clear the item's parent.
        workItems.MapPut("/{identifier}/parent", SetWorkItemParentAsync)
            .RequiresOrganizationPermission(PermissionCatalog.WorkItemsLinkWrite)
            .Produces<SetWorkItemParentResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // mod-005 § 2.1. Delete: moves the item into «Удалена» and records the way back on its row. A sub-command, not
        // `DELETE …/{identifier}` (plan-backend D-P1): the item stays, read-only, until a purge removes it. No body.
        workItems.MapPost("/{identifier}/delete", DeleteWorkItemAsync)
            .RequiresOrganizationPermission(PermissionCatalog.WorkItemsWorkItemDelete)
            .Produces<WorkItemView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // mod-005 § 2.2. Restore: takes the item out of «Удалена» into the chosen state, else the held one, else the type's
        // first. The body is optional (`stateId` only); the route holds the same code as Delete.
        workItems.MapPost("/{identifier}/restore", RestoreWorkItemAsync)
            .RequiresOrganizationPermission(PermissionCatalog.WorkItemsWorkItemDelete)
            .Produces<WorkItemView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // mod-005 § 2.3. Purge: removes a deleted item for good, in one transaction. The organization administrator's code
        // alone; 204, and nothing of the item is kept. No body.
        workItems.MapPost("/{identifier}/purge", PurgeWorkItemAsync)
            .RequiresOrganizationPermission(PermissionCatalog.WorkItemsWorkItemPurge)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // feat-001 § 3.4. The «Родитель» picker: this item's possible parents.
        workItems.MapGet("/{identifier}/parent-candidates", GetParentCandidatesAsync)
            .RequiresOrganizationPermission(PermissionCatalog.WorkItemsWorkItemRead)
            .Produces<ParentCandidatesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // § 2.5. The assignee picker: organization-level, narrowed to the organization's active members (mod-016
        // BR-M1-26). A GET with a write code is legitimate: the picker is a write control, so a viewer is refused
        // (api-contract § 4.2 row 34).
        app.MapGet("/api/organizations/{organizationId}/workitems/assignee-candidates", GetAssigneeCandidatesAsync)
            .WithTags(Tag)
            .RequiresOrganizationPermission(PermissionCatalog.WorkItemsWorkItemWrite)
            .Produces<AssigneeCandidatesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    // mod-017 api-contract § 3: the team filter is the repeated `teamId` — a team's id or `none`. feat-003 api-contract § 3 adds the
    // repeated `state` (names as stored) and `assigneeId` (a user id or `none`) and the one `search` (a keyword). Nothing is
    // validated here: a value that names nothing selects nothing, and answers alike for each such value.
    private static async Task<IResult> GetWorkItemsAsync(
        string organizationId,
        string projectId,
        int? page,
        [Microsoft.AspNetCore.Mvc.FromQuery(Name = "teamId")] string[]? teamId,
        [Microsoft.AspNetCore.Mvc.FromQuery(Name = "state")] string[]? state,
        [Microsoft.AspNetCore.Mvc.FromQuery(Name = "assigneeId")] string[]? assigneeId,
        [Microsoft.AspNetCore.Mvc.FromQuery(Name = "search")] string[]? search,
        GetWorkItemsHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(
            (await handler.Handle(new GetWorkItemsQuery(organizationId, projectId, page, teamId, state, assigneeId, search), cancellationToken)).Page);

    // feat-003 api-contract § 2.2: the chosen `state` names decide whether the people of deleted items are offered.
    private static async Task<IResult> GetAssigneeOptionsAsync(
        string organizationId,
        string projectId,
        [Microsoft.AspNetCore.Mvc.FromQuery(Name = "state")] string[]? state,
        GetAssigneeOptionsHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new GetAssigneeOptionsQuery(organizationId, projectId, state), cancellationToken)).Options);

    private static async Task<IResult> CreateWorkItemAsync(
        string organizationId, string projectId, CreateWorkItemRequest? request, CreateWorkItemHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new CreateWorkItemCommand(
                organizationId,
                projectId,
                request?.RequestId,
                request?.TypeId,
                request?.Title,
                request?.Team is { } team ? new TeamChange(team.TeamId) : null,
                request?.ParentId),
            cancellationToken);

        if (!result.Created)
        {
            return Results.Ok(result.Item);
        }

        // The organization id passed the scoped check, so it is canonical in some letter case; responses write it in lower case.
        var location = $"/api/organizations/{organizationId.ToLowerInvariant()}/projects/{result.Item.ProjectId}/workitems/{result.Item.Identifier}";

        return Results.Created(location, result.Item);
    }

    private static async Task<IResult> GetWorkItemAsync(
        string organizationId, string projectId, string identifier, GetWorkItemHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new GetWorkItemQuery(organizationId, projectId, identifier), cancellationToken)).Item);

    private static async Task<IResult> SaveWorkItemAsync(
        string organizationId,
        string projectId,
        string identifier,
        SaveWorkItemRequest? request,
        SaveWorkItemHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new SaveWorkItemCommand(
            organizationId,
            projectId,
            identifier,
            request?.Title,
            request?.Description,
            request?.StateId,
            request?.Assignee is { } assignee ? new AssigneeChange(assignee.UserId) : null,
            request?.Parent is { } parent ? new ParentChange(parent.ParentId, parent.SeenParentId) : null,
            request is null ? null : FieldsOf(request),
            request?.Team is { } team ? new TeamChange(team.TeamId) : null);

        return Results.Ok((await handler.Handle(command, cancellationToken)).Item);
    }

    /// <summary>feat-022 api-contract § 3.2: a wrapper sent is a field sent, its <c>value</c> <c>null</c> to empty it.</summary>
    private static WorkItemFieldsEdit FieldsOf(SaveWorkItemRequest request) => new()
    {
        Priority = Sent(request.Priority),
        Risk = Sent(request.Risk),
        StoryPoints = Sent(request.StoryPoints),
        Effort = Sent(request.Effort),
        BusinessValue = Sent(request.BusinessValue),
        TimeCriticality = Sent(request.TimeCriticality),
        StartDate = Sent(request.StartDate),
        TargetDate = Sent(request.TargetDate),
        Severity = Sent(request.Severity),
        Activity = Sent(request.Activity),
        OriginalEstimate = Sent(request.OriginalEstimate),
        RemainingWork = Sent(request.RemainingWork),
        CompletedWork = Sent(request.CompletedWork),
        ValueArea = Sent(request.ValueArea),
        AcceptanceCriteria = request.AcceptanceCriteria?.Select(c => c is null
            ? new AcceptanceCriterion(string.Empty, string.Empty, string.Empty)
            : new AcceptanceCriterion(c.Given ?? string.Empty, c.When ?? string.Empty, c.Then ?? string.Empty)).ToList(),
        ReproSteps = request.ReproSteps,
        SystemInfo = request.SystemInfo,
    };

    private static FieldEdit<T>? Sent<T>(FieldRequest<T>? field) => field is null ? null : new FieldEdit<T>(field.Value);

    private static async Task<IResult> SetWorkItemParentAsync(
        string organizationId,
        string projectId,
        string identifier,
        ParentChangeRequest? request,
        SetWorkItemParentHandler handler,
        CancellationToken cancellationToken)
    {
        // A missing body is {}: clear, having seen none (api-contract § 3.2).
        var change = new ParentChange(request?.ParentId, request?.SeenParentId);

        return Results.Ok(await handler.Handle(new SetWorkItemParentCommand(organizationId, projectId, identifier, change), cancellationToken));
    }

    private static async Task<IResult> DeleteWorkItemAsync(
        string organizationId, string projectId, string identifier, DeleteWorkItemHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new DeleteWorkItemCommand(organizationId, projectId, identifier), cancellationToken)).Item);

    private static async Task<IResult> PurgeWorkItemAsync(
        string organizationId, string projectId, string identifier, PurgeWorkItemHandler handler, CancellationToken cancellationToken)
    {
        await handler.Handle(new PurgeWorkItemCommand(organizationId, projectId, identifier), cancellationToken);

        return Results.NoContent();
    }

    // A missing body, `{}` and `"stateId": null` are one request: the held state (api-contract § 2.2).
    private static async Task<IResult> RestoreWorkItemAsync(
        string organizationId,
        string projectId,
        string identifier,
        RestoreWorkItemRequest? request,
        RestoreWorkItemHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new RestoreWorkItemCommand(organizationId, projectId, identifier, request?.StateId), cancellationToken)).Item);

    private static async Task<IResult> GetParentCandidatesAsync(
        string organizationId,
        string projectId,
        string identifier,
        string? text,
        bool? levelAbove,
        GetParentCandidatesHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(
            new GetParentCandidatesQuery(organizationId, projectId, identifier, text, levelAbove ?? false),
            cancellationToken));

    private static async Task<IResult> GetAssigneeCandidatesAsync(
        string organizationId, string? text, GetAssigneeCandidatesHandler handler, CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(new GetAssigneeCandidatesQuery(organizationId, text), cancellationToken));

    // --- Request bodies: every field nullable, so a missing one reaches the handler as a validation answer -----

    /// <summary>
    /// <c>team</c> is mod-017's (api-contract § 0.2), in the wrapper of <see cref="TeamRequest"/>: optional, the owning team
    /// the dialog shows; absent, the item takes the project default team. It replaces mod-006's flat <c>teamId</c>, which a body
    /// may still carry and which is ignored (unknown properties are, as everywhere).
    /// <c>parentId</c> is feat-027's (its api-contract § 3): optional, the item to create the new one beneath; a request that
    /// names a parent never reads <c>team</c>.
    /// </summary>
    private sealed record CreateWorkItemRequest(string? RequestId, string? TypeId, string? Title, TeamRequest? Team, string? ParentId);

    /// <summary>
    /// A field that is absent or <c>null</c> is not changed (BR-M3-25); <c>assignee.userId</c> <c>null</c> clears it.
    /// feat-022's fields (its api-contract § 3.2): a text as a string, <c>""</c> to empty it; every other in a
    /// <c>{ "value": … }</c> wrapper, <c>{ "value": null }</c> or <c>{}</c> to empty it. A value of the wrong JSON
    /// type fails binding (400 <c>http.bad_request</c>).
    /// </summary>
    private sealed record SaveWorkItemRequest(
        string? Title,
        string? Description,
        string? StateId,
        AssigneeRequest? Assignee,
        ParentChangeRequest? Parent,
        TeamRequest? Team,
        FieldRequest<int?>? Priority,
        FieldRequest<int?>? Risk,
        FieldRequest<decimal?>? StoryPoints,
        FieldRequest<decimal?>? Effort,
        FieldRequest<decimal?>? BusinessValue,
        FieldRequest<decimal?>? TimeCriticality,
        FieldRequest<DateOnly?>? StartDate,
        FieldRequest<DateOnly?>? TargetDate,
        FieldRequest<int?>? Severity,
        FieldRequest<string?>? Activity,
        FieldRequest<decimal?>? OriginalEstimate,
        FieldRequest<decimal?>? RemainingWork,
        FieldRequest<decimal?>? CompletedWork,
        FieldRequest<string?>? ValueArea,
        IReadOnlyList<AcceptanceCriterionRequest>? AcceptanceCriteria,
        string? ReproSteps,
        string? SystemInfo);

    /// <summary>mod-005 api-contract § 2.2: optional; <c>stateId</c> absent or <c>null</c> restores the held state, else the first.</summary>
    private sealed record RestoreWorkItemRequest(string? StateId);

    private sealed record AcceptanceCriterionRequest(string? Given, string? When, string? Then);

    /// <summary>feat-022: one sent field; <c>value</c> absent or <c>null</c> empties it.</summary>
    private sealed record FieldRequest<T>(T Value);

    private sealed record AssigneeRequest(string? UserId);

    /// <summary>
    /// mod-017 api-contract § 0.2: the owning team in a wrapper, as <c>assignee</c> is, because a flat <c>null</c> reads as
    /// «not changed». <c>{ "teamId": null }</c> and <c>{}</c> are «Без команды»; absent, or <c>null</c> in place of the wrapper,
    /// is not sent.
    /// </summary>
    private sealed record TeamRequest(string? TeamId);

    /// <summary>feat-001 api-contract § 2.3: <c>parentId</c> <c>null</c> clears; <c>seenParentId</c> <c>null</c> saw none.</summary>
    private sealed record ParentChangeRequest(string? ParentId, string? SeenParentId);
}
