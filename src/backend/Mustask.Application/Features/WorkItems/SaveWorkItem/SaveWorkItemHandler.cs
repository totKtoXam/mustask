using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.Boards;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.WorkItems.SaveWorkItem;

/// <summary>
/// One save of the form (US-3, US-4, US-5): exactly the fields sent, all of
/// them or none, with no conflict ever reported (BR-M3-25).
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.4, feat-001 api-contract
/// § 3.3, mod-016 api-contract § 4.2): the scoped check has answered for the
/// organization and the route's code; a save that carries a parent then answers
/// 403 when the role lacks <c>workitems.link.write</c>, before anything below;
/// the address from the outside in, as the read resolves it; the values
/// (400, every field reason at once — feat-022's fields included, checked by the
/// domain against the item's type and its stored dates, under the item's lock,
/// feat-022 api-contract § 3.2); the area (404); mod-017's team (404, or 409 for a team of
/// another project, <see cref="ProjectTeams.EnsureBindable"/>); then the new parent (404); the
/// state, then the assignee (409, decided by the domain), then the parent's
/// 409s (<see cref="WorkItemHierarchy.Write"/>). A refusal throws before
/// anything is flushed, and the transaction rolls back: every field, the team and the parent
/// included, stays as it was (feat-001 BR-H-6).
/// </para>
/// <para>
/// <b>The owning team</b> (mod-017 BR-OT-1, BR-OT-2, BR-OT-6, BR-OT-13): a save that names a team locks it
/// <c>FOR SHARE</c> <i>first</i> of everything it locks (the binder's lock, ADR-0028 rule 8; plan-backend § 7.2), so a deletion
/// of that team either commits before this save reads it — the team then reads deleted and the save answers
/// <c>team.not_found</c> — or waits for this save and takes the item with the rest. The team is judged after the area, on a
/// fresh read. A team that changes the item's owner drops the item's team place <i>before</i> the item is written, in the same
/// transaction: the place's key onto the item's current owner is checked at the end of the item's <c>UPDATE</c>, and the
/// item lands unplaced below every placed item on the new team's backlog, with no memory of a former place.
/// </para>
/// <para>
/// <b>Locks.</b> When the save names a team, the team <c>FOR SHARE</c>. When the save carries a parent (feat-001), the project's
/// hierarchy <c>FOR UPDATE</c> next, as every parent write takes it. Then the
/// item <c>FOR UPDATE</c>, before it is loaded: two saves of one item run one
/// after the other, and the second applies its own fields to what the first
/// stored, so a field it did not send keeps the first's value and a field both
/// sent keeps the later one (BR-M3-25). Then, when the area changes, the new
/// area <c>FOR SHARE</c>: mod-002's deletion locks it <c>FOR UPDATE</c> and asks
/// for its work items after its lock, so a deletion and a save into that area
/// never both succeed (BR-M3-24). Then the new and the stored parent
/// <c>FOR NO KEY UPDATE</c>. The project is not locked: a write beneath only
/// reads its containers' status (ADR-0026 rule 8).
/// </para>
/// </remarks>
public sealed class SaveWorkItemHandler(
    IOrganizationStructure structure,
    IProcessDefinitionReadModel processes,
    IUserDirectory directory,
    IMemberReadModel members,
    IWorkItemRepository workItems,
    IWorkItemReadModel readModel,
    ITeamBacklogPlaces teamPlaces,
    IOrganizationAccess access,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<SaveWorkItemResult> Handle(SaveWorkItemCommand command, CancellationToken cancellationToken)
    {
        // 0. The second code of a parent write (mod-016 api-contract § 4.2, feat-001 BR-H-18): the route declares
        // workitems.workitem.write, and a save that carries a parent needs workitems.link.write as well. It is checked first,
        // before the project, the item or the parent is looked at and before any 400, so a role without it learns nothing about
        // any of them and nothing changes. Under the bundles of D-8 the two codes never differ.
        if (command.Parent is not null && !access.Holds(PermissionCatalog.WorkItemsLinkWrite))
        {
            throw new ForbiddenException(OrganizationErrorCodes.AccessForbidden);
        }

        return await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address: the organization, the project, the identifier, the item and its type.
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                if (!AddressedId.TryParse(command.ProjectId, out var projectId)
                    || await structure.FindProjectAsync(projectId, ct) is not { IsReachable: true } project
                    || project.OrganizationId != organizationId)
                {
                    throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
                }

                if (!WorkItemIdentifier.TryParse(command.Identifier, out var identifier)
                    || !string.Equals(identifier.ProjectKey, project.Key, StringComparison.Ordinal))
                {
                    throw new NotFoundException(WorkItemErrorCodes.WorkItemNotFound);
                }

                var process = await processes.FindByOrganizationAsync(organizationId, ct)
                    ?? throw new InvalidOperationException($"The organization {organizationId} has no process.");

                // The binder's lock first of all (mod-017 plan-backend § 7.1): only a team that is named as an id is locked.
                if (TeamIdOf(command.Team) is { } namedTeamId)
                {
                    await workItems.LockTeamAsync(organizationId, namedTeamId, ct);
                }

                if (command.Parent is not null)
                {
                    await workItems.LockHierarchyAsync(projectId, ct);
                }

                var item = await workItems.LockAsync(projectId, identifier.Number, ct)
                    ?? throw new NotFoundException(WorkItemErrorCodes.WorkItemNotFound);

                var type = ProcessWorkItemTypes.OfItem(process, item.TypeId);

                if (!string.Equals(type.TypeKey, identifier.TypeKey, StringComparison.Ordinal))
                {
                    throw new NotFoundException(WorkItemErrorCodes.WorkItemNotFound);
                }

                // 2. The values, every field reason at once.
                var title = ValidateValues(command, item, type);

                // 3. The area: one of the item's own project, still there under the lock (BR-M3-23, BR-M3-24).
                var areaId = await LockAreaAsync(projectId, item.AreaId, command.AreaId, ct);

                // 3a. The team: a team of this project, or none, judged on a fresh read under the team's lock (mod-017
                // api-contract § 0.2). Not sent: the stored team stays.
                var team = await DecideTeamAsync(command.Team, organizationId, projectId, ct);

                // 3b. The new parent, an item of this project, and the rest of the parent write (feat-001 BR-H-1).
                var parentWrite = command.Parent is { } change
                    ? await workItems.LockParentWriteAsync(item, change.NewParentId(), change.SeenId(), ct)
                        ?? throw new NotFoundException(WorkItemErrorCodes.ParentNotFound)
                    : null;

                // 4. The state and the assignee, then the parent, which the domain decides.
                var edit = new WorkItemEdit(
                    title,
                    command.Description,
                    StateIdOf(command.StateId),
                    await AssigneeOfAsync(command.Assignee, organizationId, ct),
                    areaId,
                    command.Fields,
                    team);

                var now = clock.UtcNow;
                var previousTeamId = item.OwningTeamId;
                var changed = item.Save(edit, type, now);

                // BR-OT-6: a changed owner drops the item's team place in the same write, before the item's UPDATE (the place
                // key is checked at the end of that statement). The project's place is another table and stays.
                if (item.OwningTeamId != previousTeamId)
                {
                    await teamPlaces.DropForItemAsync(item.Id, ct);
                }

                if (parentWrite is not null)
                {
                    changed |= WorkItemHierarchy.Write(parentWrite, ProcessHierarchy.Of(process), project.Key, now);
                }

                if (changed)
                {
                    await workItems.SaveAsync(ct);
                }

                return new SaveWorkItemResult(await readModel.GetAsync(item.Id, ct));
            },
            cancellationToken);
    }

    /// <summary>
    /// The trimmed title when one was sent; throws one validation answer with every field reason, feat-022's fields
    /// keyed by their request names (its api-contract § 1).
    /// </summary>
    private static string? ValidateValues(SaveWorkItemCommand command, WorkItem item, WorkItemTypeStates type)
    {
        var errors = new Dictionary<string, IReadOnlyList<FieldError>>(StringComparer.Ordinal);
        string? title = null;

        if (command.Title is not null)
        {
            if (WorkItemText.ValidateTitle(command.Title, out var trimmed) is { } titleReason)
            {
                errors["title"] = [new FieldError(titleReason)];
            }
            else
            {
                title = trimmed;
            }
        }

        if (command.Description is not null && WorkItemText.ValidateDescription(command.Description) is { } descriptionReason)
        {
            errors["description"] = [new FieldError(descriptionReason)];
        }

        if (command.Fields is { } fields)
        {
            foreach (var reason in item.CheckFields(fields, type))
            {
                errors[RequestName(reason.Field)] = [new FieldError(reason.Code)];
            }
        }

        return errors.Count > 0
            ? throw new ValidationException(WorkItemErrorCodes.ValidationFailed, errors)
            : title;
    }

    /// <summary>
    /// <c>null</c> when no area was sent; the current area needs no lock; any
    /// other is locked <c>FOR SHARE</c> and must be an area of the project.
    /// </summary>
    private async Task<Guid?> LockAreaAsync(Guid projectId, Guid currentAreaId, string? rawAreaId, CancellationToken cancellationToken)
    {
        if (rawAreaId is null)
        {
            return null;
        }

        if (!AddressedId.TryParse(rawAreaId, out var areaId))
        {
            throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
        }

        if (areaId != currentAreaId && !await workItems.LockAreaAsync(projectId, areaId, cancellationToken))
        {
            throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
        }

        return areaId;
    }

    /// <summary>The sent team as an id to lock: <c>null</c> for a team not sent, «Без команды» and a string that is no id.</summary>
    private static Guid? TeamIdOf(TeamChange? change) =>
        change?.TeamId is { } raw && AddressedId.TryParse(raw, out var teamId) ? teamId : null;

    /// <summary>
    /// <c>null</c> when no team was sent; «Без команды» for a sent <c>null</c>; otherwise the team, which must be an active
    /// team of this project (api-contract § 0.2): a string that is no id, an unknown, foreign or deleted team, or a team of a
    /// deactivated project answers 404 <c>team.not_found</c> alike, and an active team of another active project 409
    /// <c>team.not_in_project</c>.
    /// </summary>
    private async Task<TeamEdit?> DecideTeamAsync(TeamChange? change, Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        if (change is null)
        {
            return null;
        }

        if (change.TeamId is null)
        {
            return new TeamEdit(null);
        }

        if (!AddressedId.TryParse(change.TeamId, out var teamId))
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
        }

        var found = await structure.FindTeamAsync(teamId, cancellationToken);

        ProjectTeams.EnsureBindable(
            found is null ? null : new TeamFacts(found.Id, found.OrganizationId, found.ProjectId, found.Status, found.IsReachable),
            organizationId,
            projectId);

        return new TeamEdit(teamId);
    }

    /// <summary>A sent state that is not even an id is a state the type does not have (BR-M3-21).</summary>
    private static Guid? StateIdOf(string? rawStateId)
    {
        if (rawStateId is null)
        {
            return null;
        }

        return AddressedId.TryParse(rawStateId, out var stateId)
            ? stateId
            : throw new ConflictException(WorkItemErrorCodes.StateNotAllowed);
    }

    /// <summary>
    /// The sent assignee with whether they can be assigned (BR-M3-22, mod-016 BR-M1-26): known to the directory, active
    /// there, <b>and a member of the item's organization</b> in any role. The domain decides, and refuses only a
    /// <i>newly set</i> unavailable assignee: an assignee who was set and has since left, or been disabled, stays and never
    /// refuses a save of other fields, and clearing the assignee is always allowed.
    /// </summary>
    private async Task<AssigneeEdit?> AssigneeOfAsync(AssigneeChange? change, Guid organizationId, CancellationToken cancellationToken)
    {
        if (change is null)
        {
            return null;
        }

        if (change.UserId is not { } userId)
        {
            return new AssigneeEdit(null, IsAvailable: true);
        }

        var lookup = await directory.LookupAsync([userId], cancellationToken);
        var isAvailable = lookup.Known.TryGetValue(userId, out var user)
            && user.IsActive
            && await members.IsMemberAsync(organizationId, userId, cancellationToken);

        return new AssigneeEdit(userId, isAvailable);
    }

    /// <summary>The request's name of a feat-022 field: the key of its reasons under <c>errors</c> (its api-contract § 1).</summary>
    private static string RequestName(WorkItemField field) => field switch
    {
        WorkItemField.Priority => "priority",
        WorkItemField.Risk => "risk",
        WorkItemField.StoryPoints => "storyPoints",
        WorkItemField.Effort => "effort",
        WorkItemField.BusinessValue => "businessValue",
        WorkItemField.TimeCriticality => "timeCriticality",
        WorkItemField.StartDate => "startDate",
        WorkItemField.TargetDate => "targetDate",
        WorkItemField.Severity => "severity",
        WorkItemField.Activity => "activity",
        WorkItemField.OriginalEstimate => "originalEstimate",
        WorkItemField.RemainingWork => "remainingWork",
        WorkItemField.CompletedWork => "completedWork",
        WorkItemField.ValueArea => "valueArea",
        WorkItemField.AcceptanceCriteria => "acceptanceCriteria",
        WorkItemField.ReproSteps => "reproSteps",
        WorkItemField.SystemInfo => "systemInfo",
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown work item field."),
    };

}
