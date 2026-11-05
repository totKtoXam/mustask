using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.WorkItems.DeleteWorkItem;

/// <summary>
/// Moves a work item into its type's «Удалена» and records the way back on its row (mod-005 US-1, BR-DR-1..-5, -8, -11,
/// ADR-0030 rules 1, 3 and 4).
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.4): the scoped check has answered for the organization and the route's code
/// <c>workitems.workitem.delete</c> (ADR-0027 rule 4); then the address from the outside in (<see cref="WorkItemAddress"/>);
/// then, under the item's lock, the stored state: an item already in «Удалена» is 409 <c>workitems.workitem.deleted</c>.
/// No body is read. A refusal throws before the flush and the transaction rolls back.
/// </para>
/// <para>
/// <b>Locks</b> (ADR-0030 rule 4): the item <c>FOR UPDATE</c>, taken before it is loaded, and nothing else — no team, no
/// hierarchy, no other item, no backlog place. Nothing is cascaded, so no child, link or place is read or written; the
/// backlog places stay and a deleted item is simply no member of any backlog. A save, a restore, another delete or a purge
/// of the same item runs before or after this one (<c>DeleteRestorePurgeRaceTests</c>); a reorder that holds the item
/// <c>FOR KEY SHARE</c> makes this wait for its commit. No revision check: a deletion is a separate write and is never blocked
/// by a stale form (BR-DR-11). The project is not locked: a write beneath it only reads its containers' status (ADR-0026
/// rule 8). No event is published (ADR-0026 rule 7).
/// </para>
/// </remarks>
public sealed class DeleteWorkItemHandler(
    IOrganizationStructure structure,
    IProcessDefinitionReadModel processes,
    IWorkItemRepository workItems,
    IWorkItemReadModel readModel,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<DeleteWorkItemResult> Handle(DeleteWorkItemCommand command, CancellationToken cancellationToken) =>
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

                // 3. The deletion: the domain refuses an item that is already deleted and writes the state and the three marks
                // together, nothing else (BR-DR-3).
                item.Delete(type, currentUser.SubjectId, clock.UtcNow);
                await workItems.SaveAsync(ct);

                return new DeleteWorkItemResult(await readModel.GetAsync(item.Id, ct));
            },
            cancellationToken);
}
