using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.WorkItems.RestoreWorkItem;

/// <summary>
/// Takes a work item out of «Удалена» into the state the user chose, else the state it held, else its type's first, and
/// clears the three marks (mod-005 US-4, BR-DR-10, BR-DR-11, ADR-0030 rules 1 and 4).
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.4, § 2.2): the scoped check has answered for the organization and the route's
/// code <c>workitems.workitem.delete</c> (the same code as Delete); then the address from the outside in
/// (<see cref="WorkItemAddress"/>); then, under the item's lock, on the stored state: 409 <c>workitems.workitem.not_deleted</c>
/// — whatever <c>stateId</c> names — and then 409 <c>workitems.workitem.state_not_allowed</c> for a <c>stateId</c> that is
/// malformed, unknown, of another type or organization, or the type's «Удалена». A refusal changes nothing.
/// </para>
/// <para>
/// <b>Locks and hierarchy</b> (ADR-0030 rule 4): the item <c>FOR UPDATE</c> and nothing else. <b>No hierarchy check</b>:
/// nothing is read of a parent, a child or a place, and a restore is never refused for a cycle or a depth (no write adds a link
/// to or from a deleted item, and the checks count stored links through it). The item returns to every surface it qualifies
/// for as stored — its owning team, its kept links, its backlog place where mod-006 puts an item that comes back. No revision
/// check: a restore is a separate write (BR-DR-11).
/// </para>
/// </remarks>
public sealed class RestoreWorkItemHandler(
    IOrganizationStructure structure,
    IProcessDefinitionReadModel processes,
    IWorkItemRepository workItems,
    IWorkItemReadModel readModel,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<RestoreWorkItemResult> Handle(RestoreWorkItemCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address, from the outside in.
                var address = await WorkItemAddress.ResolveAsync(structure, command.OrganizationId, command.ProjectId, command.Identifier, ct);

                var process = await processes.FindByOrganizationAsync(address.OrganizationId, ct)
                    ?? throw new InvalidOperationException($"The organization {address.OrganizationId} has no process.");

                // 2. The item locked FOR UPDATE and loaded tracked: what is decided below is the stored state.
                var item = await workItems.LockAsync(address.ProjectId, address.Identifier.Number, ct)
                    ?? throw new NotFoundException(WorkItemErrorCodes.WorkItemNotFound);

                var type = ProcessWorkItemTypes.OfItem(process, item.TypeId);

                WorkItemAddress.EnsureTypeKey(type, address.Identifier);

                // 3. The restore: not deleted first, then the chosen state; the domain writes the state and clears the marks.
                item.Restore(type, ChosenState(command.StateId), clock.UtcNow);
                await workItems.SaveAsync(ct);

                return new RestoreWorkItemResult(await readModel.GetAsync(item.Id, ct));
            },
            cancellationToken);

    /// <summary>
    /// <c>null</c> when no state was sent; the id when it is a canonical, non-zero one; otherwise <see cref="Guid.Empty"/>, a
    /// value that names nothing and is never allowed (as <see cref="ParentChange.SeenId"/> does for a parent).
    /// </summary>
    private static Guid? ChosenState(string? rawStateId)
    {
        if (rawStateId is null)
        {
            return null;
        }

        return AddressedId.TryParse(rawStateId, out var stateId) ? stateId : Guid.Empty;
    }
}
