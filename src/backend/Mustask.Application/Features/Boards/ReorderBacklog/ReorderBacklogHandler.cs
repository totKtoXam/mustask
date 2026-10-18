using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Boards;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Boards.ReorderBacklog;

/// <summary>
/// Moves one item of a team's backlog to the top, to the bottom, or directly
/// after another item (mod-006 US-2, feat-040 US-3, BR-M4-25), giving every item of the backlog — of every active project of
/// the organization — the place it then stands at and dropping the places of items that have left it (BR-M4-24, BR-TS-7).
/// A reorder changes no field of any work item and touches no project's own order.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.4): the address from the outside in, as the backlog's read — the team is
/// the organization's own, with no project in the address; the position (400); then, under the locks, the team again and the
/// stale check (409).
/// </para>
/// <para>
/// <b>Locks</b> (ADR-0011; mod-017 plan-backend § 7, ADR-0028 rule 8, feat-040 plan-backend PD-8): the team <c>FOR SHARE</c> — a
/// write that binds to a team locks it and re-checks it is active, so a team
/// deleted meanwhile answers not found and a deletion waits for the reorder —
/// then the backlog's member items <c>FOR KEY SHARE</c> (a save that changes the owner of a member
/// locks it <c>FOR UPDATE</c>, so the two end in one order and the places this reorder writes name
/// a team that owns their items, BR-OT-13 (3); an unlink of one of the team's projects reassigns the owned items the same way, so
/// it and a reorder end in one order across projects, BR-TS-7), then
/// the backlog's order row <c>FOR UPDATE</c>, then the members read again in a locking statement. Reorders of one backlog
/// therefore run one after another in arrival order, and each reads the order
/// as the previous one committed it, in a new statement under READ COMMITTED:
/// two reorders of different items both take effect, and of two of the same
/// item the later decides its place (last write wins, no token, D-2).
/// </para>
/// </remarks>
public sealed class ReorderBacklogHandler(
    IOrganizationStructure structure,
    IProcessDefinitionReadModel processes,
    IBacklogRepository backlogs,
    IUnitOfWork unitOfWork)
{
    public async Task Handle(ReorderBacklogCommand command, CancellationToken cancellationToken)
    {
        // 1. The address, from the outside in.
        if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(command.TeamId, out var teamId)
            || await structure.FindTeamAsync(organizationId, teamId, cancellationToken) is not { IsReachable: true })
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
        }

        if (!BoardLevelNames.TryParse(command.Level, out var level)
            || await processes.FindByOrganizationAsync(organizationId, cancellationToken) is not { } process
            || level.Resolve(process.BacklogLevels.Select(row => new ProcessLevel(row.Id, row.Rank, row.Kind))) is not { } resolved)
        {
            throw new NotFoundException(BoardErrorCodes.LevelNotFound);
        }

        // 2. The position: a panel defect if it is none of the three words.
        Guid? afterItemId = AddressedId.TryParse(command.AfterItemId, out var anchor) ? anchor : null;

        if (!StackRankPosition.TryCreate(command.Position, afterItemId, out var position))
        {
            throw new ValidationException(
                BoardErrorCodes.ValidationFailed,
                new Dictionary<string, IReadOnlyList<FieldError>>(StringComparer.Ordinal)
                {
                    ["position"] = [new FieldError(BoardErrorCodes.PositionInvalid)],
                });
        }

        // The reorder is of the whole backlog: its scope carries no project filter (BR-TS-8).
        var scope = TeamBacklogScope.Of(teamId, resolved.Id, process.WorkItemTypes.Where(type => type.BacklogLevelId == resolved.Id));

        _ = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 3. The locks, then the order as the previous reorder committed it. A member that changed owner before the
                // members were locked is no member of this backlog now (mod-017 BR-OT-6, BR-OT-13 (3)).
                if (!await backlogs.LockAsync(organizationId, scope, ct))
                {
                    throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                }

                var order = StackRank.Order(await backlogs.ReadEntriesAsync(organizationId, scope, ct));

                // 4. The move: a malformed or unknown item names nothing on this backlog, which is stale (BR-M4-26) — an
                // item that has changed its owning team, before the reorder or while it waited, one of another team, of a
                // deactivated project, in the Removed category or of another organization included, all alike (BR-TS-7).
                if (!AddressedId.TryParse(command.ItemId, out var itemId))
                {
                    throw new ConflictException(BoardErrorCodes.ReorderStale);
                }

                var moved = StackRank.Move(order, itemId, position);

                // 5. Every item at its place, the places of items that left dropped.
                await backlogs.SaveOrderAsync(organizationId, scope, moved, ct);

                return true;
            },
            cancellationToken);
    }
}
