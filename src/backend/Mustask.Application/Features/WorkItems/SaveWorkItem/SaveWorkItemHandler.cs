using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.Organizations;
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
/// § 3.3): the address from the outside in, as the read resolves it; the values
/// (400, every field reason at once); the area, then the new parent (404); the
/// state, then the assignee (409, decided by the domain), then the parent's
/// 409s (<see cref="WorkItemHierarchy.Write"/>). A refusal throws before
/// anything is flushed, and the transaction rolls back: every field, the parent
/// included, stays as it was (feat-001 BR-H-6).
/// </para>
/// <para>
/// <b>Locks.</b> When the save carries a parent (feat-001), the project's
/// hierarchy <c>FOR UPDATE</c> first, as every parent write takes it. Then the
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
    IWorkItemRepository workItems,
    IWorkItemReadModel readModel,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<SaveWorkItemResult> Handle(SaveWorkItemCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
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

                if (command.Parent is not null)
                {
                    await workItems.LockHierarchyAsync(projectId, ct);
                }

                var item = await workItems.LockAsync(projectId, identifier.Number, ct)
                    ?? throw new NotFoundException(WorkItemErrorCodes.WorkItemNotFound);

                var type = ItemType(process, item.TypeId);

                if (!string.Equals(type.TypeKey, identifier.TypeKey, StringComparison.Ordinal))
                {
                    throw new NotFoundException(WorkItemErrorCodes.WorkItemNotFound);
                }

                // 2. The values, every field reason at once.
                var title = ValidateValues(command);

                // 3. The area: one of the item's own project, still there under the lock (BR-M3-23, BR-M3-24).
                var areaId = await LockAreaAsync(projectId, item.AreaId, command.AreaId, ct);

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
                    await AssigneeOfAsync(command.Assignee, ct),
                    areaId);

                var now = clock.UtcNow;
                var changed = item.Save(edit, type, now);

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

    /// <summary>The trimmed title when one was sent; throws one validation answer with every field reason.</summary>
    private static string? ValidateValues(SaveWorkItemCommand command)
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

    /// <summary>The sent assignee with whether the directory knows them as active (BR-M3-22); the domain decides.</summary>
    private async Task<AssigneeEdit?> AssigneeOfAsync(AssigneeChange? change, CancellationToken cancellationToken)
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
        var isAvailable = lookup.Known.TryGetValue(userId, out var user) && user.IsActive;

        return new AssigneeEdit(userId, isAvailable);
    }

    /// <summary>The item's own type in the organization's process. It is always there: the type never changes (BR-M3-1).</summary>
    private static WorkItemTypeStates ItemType(ProcessDefinitionRow process, Guid typeId)
    {
        var type = process.WorkItemTypes.SingleOrDefault(candidate => candidate.Id == typeId)
            ?? throw new InvalidOperationException($"The type {typeId} of a work item is not in its organization's process.");

        return new WorkItemTypeStates(
            type.Id,
            type.Key,
            type.States.Select(state => new WorkItemTypeState(state.Id, state.Position, state.Category)).ToList());
    }
}
