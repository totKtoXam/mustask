using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.WorkItems.CreateWorkItem;

/// <summary>
/// The create dialog's submission (US-1): a new item of the chosen type with
/// only a title, in its type's initial state, unassigned, with an empty
/// description, in the project's root area (BR-M3-20) — or, created from a
/// team's backlog or board, in that team's default area as it stands
/// (mod-006 BR-M4-21); or, created from a backlog row, beneath that row's item
/// and in its area as stored (feat-027 BR-T-10) — numbered from the project's
/// one sequence (BR-M3-5).
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.4, feat-027 api-contract § 3,
/// mod-016 api-contract § 4.2): the scoped check has answered for the organization
/// and the route's code (<c>workitems.workitem.write</c>) before the handler runs;
/// then, when the create carries a <c>parentId</c>, the second code — a parent
/// write needs <c>workitems.link.write</c> too, and its 403 comes before every
/// other answer about the parent or the project; then the address from the
/// outside in (the project must be the organization's and reachable);
/// the values, with every field reason at once; the type; the replay; the team,
/// when one is named (mod-006 api-contract § 7); the parent, when one is named —
/// its not-found, then the domain's 409s (the deleted parent, the level, the
/// depth bound of feat-027 D-9); then the creation.
/// </para>
/// <para>
/// <b>The number and the replay</b> (BR-M3-5, BR-M3-26). The project's counter
/// row is locked <c>FOR UPDATE</c> first, so the creations of a project run
/// one after another from there to their commit, and each takes one more than
/// the last number issued. The replay check comes after that lock, as a new
/// statement under READ COMMITTED: a submission that arrives twice at the same
/// moment waits for the first to commit, then finds its item and answers it,
/// with no number consumed. The unique index on the submission key is the
/// backstop. The root area needs no lock: it is never deleted (BR-M1-11).
/// </para>
/// <para>
/// <b>The parent</b> (feat-027 api-contract § 3, "Concurrency"): after the
/// number's and the team's locks, the parent write's own order — the project's
/// hierarchy, then the parent as a link end — so the parent is read as the last
/// parent write, save or deletion of it committed. Its area needs no lock: an
/// area that holds an item is never deleted (mod-003 § 6), and the parent's lock
/// keeps it there until the commit. A refusal rolls the transaction back, the
/// number's increment with it.
/// </para>
/// </remarks>
public sealed class CreateWorkItemHandler(
    IOrganizationStructure structure,
    IProcessDefinitionReadModel processes,
    IWorkItemRepository workItems,
    IWorkItemReadModel readModel,
    IOrganizationAccess access,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<CreateWorkItemResult> Handle(CreateWorkItemCommand command, CancellationToken cancellationToken)
    {
        // 0. The second code of a parent write (mod-016 api-contract § 4.2): checked first, before the project or the parent is
        // looked at, so a role without it learns nothing about either. Under the bundles of D-8 the two codes never differ.
        if (command.ParentId is not null && !access.Holds(PermissionCatalog.WorkItemsLinkWrite))
        {
            throw new ForbiddenException(OrganizationErrorCodes.AccessForbidden);
        }

        return await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address.
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

                // 2. The values, every field reason at once.
                var errors = new Dictionary<string, IReadOnlyList<FieldError>>(StringComparer.Ordinal);

                if (!AddressedId.TryParse(command.RequestId, out var requestId))
                {
                    errors["requestId"] = [new FieldError(WorkItemErrorCodes.RequestIdInvalid)];
                }

                if (WorkItemText.ValidateTitle(command.Title, out var title) is { } titleReason)
                {
                    errors["title"] = [new FieldError(titleReason)];
                }

                if (errors.Count > 0)
                {
                    throw new ValidationException(WorkItemErrorCodes.ValidationFailed, errors);
                }

                // 3. The type: one of the organization's process (BR-M3-1).
                if (await processes.FindByOrganizationAsync(organizationId, ct) is not { } process
                    || TypeOf(process, command.TypeId) is not { } type)
                {
                    throw new NotFoundException(WorkItemErrorCodes.TypeNotFound);
                }

                // 4. The number, and the replay after its lock.
                await workItems.LockNumberSequenceAsync(projectId, ct);

                if (await workItems.FindCreatedByRequestAsync(projectId, requestId, ct) is { } existingId)
                {
                    return new CreateWorkItemResult(await readModel.GetAsync(existingId, ct), Created: false);
                }

                // 5. The area: the root, or the team's default as it stands now (mod-006 BR-M4-21), read under the
                // team's lock. A team named but malformed, unknown, of another project or deleted answers not found.
                var areaId = project.RootAreaId;

                if (command.TeamId is not null)
                {
                    if (!AddressedId.TryParse(command.TeamId, out var teamId)
                        || await workItems.LockTeamDefaultAreaAsync(projectId, teamId, ct) is not { } defaultAreaId)
                    {
                        throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                    }

                    areaId = defaultAreaId;
                }

                // 6. feat-027: the parent, under the parent write's order — the hierarchy, then the parent itself. One
                // malformed, unknown or of another project answers alike (BR-H-1); its area then wins over the team's.
                WorkItem? parent = null;
                IReadOnlyCollection<HierarchyLink> parentAncestry = [];

                if (command.ParentId is not null)
                {
                    if (!AddressedId.TryParse(command.ParentId, out var parentId))
                    {
                        throw new NotFoundException(WorkItemErrorCodes.ParentNotFound);
                    }

                    await workItems.LockHierarchyAsync(projectId, ct);

                    parent = await workItems.LockNewParentAsync(projectId, parentId, ct)
                        ?? throw new NotFoundException(WorkItemErrorCodes.ParentNotFound);
                    parentAncestry = await workItems.AncestryAsync(projectId, parent.Id, ct);
                }

                var number = await workItems.TakeNextNumberAsync(projectId, ct);

                var item = parent is null
                    ? WorkItem.Create(projectId, number, type, areaId, title, requestId, clock.UtcNow, currentUser.SubjectId)
                    : WorkItemHierarchy.CreateChild(
                        parent, parentAncestry, ProcessHierarchy.Of(process), number, type, title, requestId, clock.UtcNow, currentUser.SubjectId);

                workItems.Add(item);
                await workItems.SaveAsync(ct);

                return new CreateWorkItemResult(await readModel.GetAsync(item.Id, ct), Created: true);
            },
            cancellationToken);
    }

    /// <summary>The type of the organization's process with that id, or <c>null</c> for a malformed, unknown or foreign one.</summary>
    private static WorkItemTypeStates? TypeOf(ProcessDefinitionRow process, string? rawTypeId)
    {
        if (!AddressedId.TryParse(rawTypeId, out var typeId))
        {
            return null;
        }

        var type = process.WorkItemTypes.SingleOrDefault(candidate => candidate.Id == typeId);

        return type is null
            ? null
            : new WorkItemTypeStates(
                type.Id,
                type.Key,
                type.Appearance,
                type.States.Select(state => new WorkItemTypeState(state.Id, state.Position, state.Category)).ToList());
    }
}
