using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.WorkItems.SetWorkItemParent;

/// <summary>
/// The parent write (feat-001 BR-H-3): set, replace or clear one item's parent,
/// from the backlog's menu (US-5) or on its own. The rules are the domain's
/// (<see cref="WorkItemHierarchy.Write"/>), the same decision the form's save
/// makes.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 3.2): the address from the outside
/// in, as the read resolves it; the new parent's not-found; then the domain's
/// 409s in the spec's order. A refusal throws before anything is flushed, and the
/// transaction rolls back: the old link stays.
/// </para>
/// <para>
/// <b>Locks</b> (status.md § Plan A3): the project's hierarchy <c>FOR UPDATE</c>
/// first, so parent writes of one project run one after another and each decides
/// on what the previous one committed — the stale check of BR-H-4 and the cycle
/// check alike; then the item <c>FOR UPDATE</c>, as mod-003's save takes it, so
/// a save of the item's other fields waits or is waited on; then the new and the
/// stored parent <c>FOR NO KEY UPDATE</c>.
/// </para>
/// </remarks>
public sealed class SetWorkItemParentHandler(
    IOrganizationStructure structure,
    IProcessDefinitionReadModel processes,
    IWorkItemRepository workItems,
    IWorkItemReadModel readModel,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<SetWorkItemParentResponse> Handle(SetWorkItemParentCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address: the organization, the project, the identifier.
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
                var hierarchy = ProcessHierarchy.Of(process);

                // 2. The project's hierarchy, then the item and its type.
                await workItems.LockHierarchyAsync(projectId, ct);

                var item = await workItems.LockAsync(projectId, identifier.Number, ct)
                    ?? throw new NotFoundException(WorkItemErrorCodes.WorkItemNotFound);

                if (!string.Equals(hierarchy.Of(item.TypeId).Key, identifier.TypeKey, StringComparison.Ordinal))
                {
                    throw new NotFoundException(WorkItemErrorCodes.WorkItemNotFound);
                }

                // 3. The new parent, an item of this project, and the rest of the write; then the domain decides.
                var write = await workItems.LockParentWriteAsync(item, command.Change.NewParentId(), command.Change.SeenId(), ct)
                    ?? throw new NotFoundException(WorkItemErrorCodes.ParentNotFound);

                if (WorkItemHierarchy.Write(write, hierarchy, project.Key, clock.UtcNow))
                {
                    await workItems.SaveAsync(ct);
                }

                return new SetWorkItemParentResponse(item.ParentId is { } parentId ? await readModel.GetRefAsync(parentId, ct) : null);
            },
            cancellationToken);
}
