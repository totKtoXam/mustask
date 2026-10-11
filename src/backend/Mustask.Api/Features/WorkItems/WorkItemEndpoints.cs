using Mustask.Api.Authorization;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.WorkItems.CreateWorkItem;
using Mustask.Application.Features.WorkItems.GetAssigneeCandidates;
using Mustask.Application.Features.WorkItems.GetParentCandidates;
using Mustask.Application.Features.WorkItems.GetWorkItem;
using Mustask.Application.Features.WorkItems.GetWorkItems;
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
/// mod-002's and mod-001's do, behind the authenticated guard and mod-002's
/// organization-context filter (BR-M3-27). Route parameters are plain strings
/// with no constraint, so a malformed id or identifier reaches the handler and
/// gets its level's not-found code instead of a routing 404.
/// </summary>
public static class WorkItemEndpoints
{
    private const string Tag = "WorkItems";

    private static readonly string ReadAccess =
        $"Declares {OrganizationPermissionCatalog.WorkItemsWorkItemRead}, an organization-scoped code that no token carries "
        + "and nothing checks yet (BR-M3-27): any signed-in user, until mod-016 retrofits the check; a non-member then "
        + "gets the not-found answer, not a 403.";

    private static readonly string WriteAccess =
        $"Declares {OrganizationPermissionCatalog.WorkItemsWorkItemWrite}, an organization-scoped code that no token carries "
        + "and nothing checks yet (BR-M3-27): any signed-in user, until mod-016 retrofits the check; a non-member then "
        + "gets the not-found answer, not a 403.";

    private static readonly string CreateAccess =
        $"{WriteAccess} A `parentId` in the create is a parent write, which mod-016 also checks against "
        + $"{OrganizationPermissionCatalog.WorkItemsLinkWrite} (feat-027 BR-T-17).";

    private static readonly string SaveAccess =
        $"{WriteAccess} A `parent` in the save is a parent write, which mod-016 also checks against "
        + $"{OrganizationPermissionCatalog.WorkItemsLinkWrite} (feat-001 BR-H-18).";

    private static readonly string LinkAccess =
        $"Declares {OrganizationPermissionCatalog.WorkItemsLinkWrite}, an organization-scoped code that no token carries "
        + "and nothing checks yet (feat-001 BR-H-18): any signed-in user, until mod-016 retrofits the check; a non-member "
        + "then gets the not-found answer, not a 403.";

    public static IEndpointRouteBuilder MapWorkItemEndpoints(this IEndpointRouteBuilder app)
    {
        var workItems = app.MapGroup("/api/organizations/{organizationId}/projects/{projectId}/workitems").WithTags(Tag);

        // § 2.1. The project's list.
        workItems.MapGet("/", GetWorkItemsAsync)
            .AuthenticatedOnly(ReadAccess)
            .RequiresOrganizationContext()
            .Produces<Paging<WorkItemListItemView>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // § 2.2. Create; a replay of a submission answers 200 with the item it created. With feat-027's `parentId`
        // it creates the item beneath that parent, refused by the parent write's 404 and 409s.
        workItems.MapPost("/", CreateWorkItemAsync)
            .AuthenticatedOnly(CreateAccess)
            .RequiresOrganizationContext()
            .Produces<WorkItemView>(StatusCodes.Status201Created)
            .Produces<WorkItemView>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // § 2.3. One item by its identifier.
        workItems.MapGet("/{identifier}", GetWorkItemAsync)
            .AuthenticatedOnly(ReadAccess)
            .RequiresOrganizationContext()
            .Produces<WorkItemView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // § 2.4. Save the form: exactly the fields sent; feat-001 § 3.3 adds the parent.
        workItems.MapPatch("/{identifier}", SaveWorkItemAsync)
            .AuthenticatedOnly(SaveAccess)
            .RequiresOrganizationContext()
            .Produces<WorkItemView>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // feat-001 § 3.2. The parent write: set, replace or clear the item's parent.
        workItems.MapPut("/{identifier}/parent", SetWorkItemParentAsync)
            .AuthenticatedOnly(LinkAccess)
            .RequiresOrganizationContext()
            .Produces<SetWorkItemParentResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // feat-001 § 3.4. The «Родитель» picker: this item's possible parents.
        workItems.MapGet("/{identifier}/parent-candidates", GetParentCandidatesAsync)
            .AuthenticatedOnly(ReadAccess)
            .RequiresOrganizationContext()
            .Produces<ParentCandidatesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // § 2.5. The assignee picker: organization-level, as mod-016 will narrow it to the organization's members.
        app.MapGet("/api/organizations/{organizationId}/workitems/assignee-candidates", GetAssigneeCandidatesAsync)
            .WithTags(Tag)
            .AuthenticatedOnly(WriteAccess)
            .RequiresOrganizationContext()
            .Produces<AssigneeCandidatesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetWorkItemsAsync(
        string organizationId, string projectId, int? page, GetWorkItemsHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.Handle(new GetWorkItemsQuery(organizationId, projectId, page), cancellationToken)).Page);

    private static async Task<IResult> CreateWorkItemAsync(
        string organizationId, string projectId, CreateWorkItemRequest? request, CreateWorkItemHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new CreateWorkItemCommand(
                organizationId, projectId, request?.RequestId, request?.TypeId, request?.Title, request?.TeamId, request?.ParentId),
            cancellationToken);

        if (!result.Created)
        {
            return Results.Ok(result.Item);
        }

        // The organization id passed the filter, so it is canonical in some letter case; responses write it in lower case.
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
            request?.AreaId,
            request?.Parent is { } parent ? new ParentChange(parent.ParentId, parent.SeenParentId) : null,
            request is null ? null : FieldsOf(request));

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
        AcceptanceCriteria = request.AcceptanceCriteria,
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
    /// <c>teamId</c> is mod-006's (its api-contract § 7): optional, the team surface the item is created from.
    /// <c>parentId</c> is feat-027's (its api-contract § 3): optional, the item to create the new one beneath.
    /// </summary>
    private sealed record CreateWorkItemRequest(string? RequestId, string? TypeId, string? Title, string? TeamId, string? ParentId);

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
        string? AreaId,
        ParentChangeRequest? Parent,
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
        string? AcceptanceCriteria,
        string? ReproSteps,
        string? SystemInfo);

    /// <summary>feat-022: one sent field; <c>value</c> absent or <c>null</c> empties it.</summary>
    private sealed record FieldRequest<T>(T Value);

    private sealed record AssigneeRequest(string? UserId);

    /// <summary>feat-001 api-contract § 2.3: <c>parentId</c> <c>null</c> clears; <c>seenParentId</c> <c>null</c> saw none.</summary>
    private sealed record ParentChangeRequest(string? ParentId, string? SeenParentId);
}
