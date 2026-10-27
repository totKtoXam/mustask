using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.Boards;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Exceptions;
using Microsoft.Extensions.Logging;

namespace Mustask.Application.Features.WorkItems.PurgeWorkItem;

/// <summary>
/// Removes a deleted work item for good, by an administrator, in one transaction (mod-005 US-6, BR-DR-12..-15, ADR-0030 rule 5).
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.4, § 2.3): the scoped check has answered for the organization and the route's
/// code <c>workitems.workitem.purge</c> (the administrator's alone); the address from the outside in
/// (<see cref="WorkItemAddress"/>); then 409 <c>workitems.workitem.not_deleted</c> for an item outside «Удалена» — first on a read
/// without a lock, so a live item is refused before any pre-flight runs, and again under the locks, where a restore that got
/// there first is refused too; a purge or a restore after a purge finds no row and answers 404. A pre-flight of a later line's
/// step may add its own refusal (none is registered in this line).
/// </para>
/// <para>
/// <b>Locks</b> (ADR-0028 rule 8, ADR-0030 rule 5), in this order: the project's hierarchy <c>FOR UPDATE</c>, as every
/// link-changing write takes it, so the children are a stable set and no cycle or depth check reads a link being removed; then
/// the item and its direct children — deleted ones too — in <b>one statement</b> in id order, <c>FOR NO KEY UPDATE</c>, the
/// order every other locker of several items keeps, so a purge and a team's deletion or unlink never deadlock and a reorder's
/// <c>FOR KEY SHARE</c> is never waited on; then the rows the purge changes, which it already holds. The final <c>DELETE</c>
/// takes the item's row at <c>FOR UPDATE</c> strength, which only a <c>KEY SHARE</c> holder of that row could make wait, and a
/// deleted item has none (it is no member of a reorder's locking read, and a reorder that held it made the Delete wait).
/// </para>
/// <para>
/// <b>What goes</b>: the registered purge steps run first, inside the transaction (ADR-0030 rule 7); then the item's team place
/// (before the delete: the place's SQL-only owner key onto the item is checked at the end of a statement, as for a change of
/// owner); then one set-based <c>UPDATE</c> clears the children's link; then the item itself, and its project place with it by the
/// cascading key. Nothing of the item is kept: no tombstone, no copy; the project's number counter is never decremented, so the
/// number is not issued again. After the commit — never inside — one structured log line says who purged what, and who deleted
/// it and when (the marks go with the row; no title, no content): a rolled-back purge logs nothing.
/// </para>
/// </remarks>
public sealed class PurgeWorkItemHandler(
    IOrganizationStructure structure,
    IProcessDefinitionReadModel processes,
    IWorkItemRepository workItems,
    IWorkItemReadModel readModel,
    ITeamBacklogPlaces teamPlaces,
    IEnumerable<IWorkItemPurgeStep> steps,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork,
    ILogger<PurgeWorkItemHandler> logger)
{
    public async Task Handle(PurgeWorkItemCommand command, CancellationToken cancellationToken)
    {
        // 1. The address, from the outside in, and the item's type in the organization's process.
        var address = await WorkItemAddress.ResolveAsync(structure, command.OrganizationId, command.ProjectId, command.Identifier, cancellationToken);

        var process = await processes.FindByOrganizationAsync(address.OrganizationId, cancellationToken)
            ?? throw new InvalidOperationException($"The organization {address.OrganizationId} has no process.");

        // 2. The item read once without a lock (plan-backend D-P9): a missing item or another type key is 404, and a live item is
        // refused here, before any pre-flight runs. The transaction decides again under the locks.
        var head = await readModel.FindHeadAsync(address.ProjectId, address.Identifier.Number, cancellationToken)
            ?? throw new NotFoundException(WorkItemErrorCodes.WorkItemNotFound);

        var type = ProcessWorkItemTypes.OfItem(process, head.TypeId);

        WorkItemAddress.EnsureTypeKey(type, address.Identifier);

        if (!type.IsRemoved(head.StateId))
        {
            throw new ConflictException(WorkItemErrorCodes.WorkItemNotDeleted);
        }

        // 3. The pre-flights, outside the transaction: they may refuse and may do I/O.
        var now = clock.UtcNow;
        var identifier = WorkItemIdentifier.Format(address.ProjectKey, type.TypeKey, address.Identifier.Number);
        var request = new WorkItemPurgeRequest(address.OrganizationId, address.ProjectId, head.Id, identifier, currentUser.SubjectId, now);

        foreach (var step in steps)
        {
            await step.PreflightAsync(request, cancellationToken);
        }

        // 4. The purge, in one transaction.
        var deletion = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 4a. The hierarchy, as every link-changing write takes it (ADR-0028 rule 8 step 5).
                await workItems.LockHierarchyAsync(address.ProjectId, ct);

                // 4b. The item and its children by id, in one statement. No row: a purge got there first.
                var childIds = await workItems.LockForPurgeAsync(address.ProjectId, head.Id, ct)
                    ?? throw new NotFoundException(WorkItemErrorCodes.WorkItemNotFound);

                // 4c. The item as it is under the locks: a restore that got there first leaves it live.
                var item = await workItems.FindLockedAsync(address.ProjectId, head.Id, ct)
                    ?? throw new NotFoundException(WorkItemErrorCodes.WorkItemNotFound);

                item.EnsureDeleted(type);

                // 4d. The steps of the lines that keep data of their own, in registration order, with the children's ids.
                var purge = new WorkItemPurge(request, childIds);

                foreach (var step in steps)
                {
                    await step.PurgeAsync(purge, ct);
                }

                // 4e. The item's team place, before the delete; 4f. its children let go of it; 4g. the item itself.
                await teamPlaces.DropForItemAsync(item.Id, ct);
                await workItems.ReleaseChildrenAsync(address.ProjectId, item.Id, now, ct);

                // The deletion's marks, read under the lock before the row goes: the purge removes the only record of them.
                var marks = new DeletionMarks(item.DeletedBy, item.DeletedAt);

                workItems.Remove(item);
                await workItems.SaveAsync(ct);

                return marks;
            },
            cancellationToken);

        // 5. The whole trace (BR-DR-15, security review S-1): after the commit, and never the title or any content. It names the
        // deleter and the deletion's time as well, because the purge takes the marks with the row.
        logger.LogInformation(
            "Work item {Identifier} purged by {UserId} in project {ProjectId} of organization {OrganizationId}, deleted by {DeletedBy} at {DeletedAt:O}.",
            identifier,
            currentUser.SubjectId,
            address.ProjectId,
            address.OrganizationId,
            deletion.By,
            deletion.At);
    }

    /// <summary>Who deleted the item and when, as the row held them under the purge's lock; both are <c>null</c> for an item with no marks.</summary>
    private sealed record DeletionMarks(string? By, DateTimeOffset? At);
}
