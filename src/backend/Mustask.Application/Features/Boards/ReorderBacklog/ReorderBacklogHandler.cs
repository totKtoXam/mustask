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
/// after another item (US-2, BR-M4-25), giving every item of the backlog the
/// place it then stands at and dropping the places of items that have left it
/// (BR-M4-24). A reorder changes no field of any work item.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.4): the address from the
/// outside in, as the backlog's read; the position (400); then, under the
/// locks, the team again and the stale check (409).
/// </para>
/// <para>
/// <b>Locks</b> (status.md § Plan 5, ADR-0011): the team <c>FOR SHARE</c> — a
/// write that binds to a team locks it and re-checks it is active, so a team
/// deleted meanwhile answers not found and a deletion waits for the reorder —
/// then the backlog's order row <c>FOR UPDATE</c>. Reorders of one backlog
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

        if (!AddressedId.TryParse(command.ProjectId, out var projectId)
            || await structure.FindProjectAsync(projectId, cancellationToken) is not { IsReachable: true } project
            || project.OrganizationId != organizationId)
        {
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }

        if (!AddressedId.TryParse(command.TeamId, out var teamId)
            || await structure.FindTeamAsync(teamId, cancellationToken) is not { IsReachable: true } team
            || team.ProjectId != projectId)
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

        var scope = BacklogScope.Of(projectId, teamId, resolved.Id, process.WorkItemTypes.Where(type => type.BacklogLevelId == resolved.Id));

        _ = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 3. The locks, then the order as the previous reorder committed it.
                if (!await backlogs.LockAsync(projectId, teamId, resolved.Id, ct))
                {
                    throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                }

                var order = StackRank.Order(await backlogs.ReadEntriesAsync(scope, ct));

                // 4. The move: a malformed or unknown item names nothing on this backlog, which is stale (BR-M4-26).
                if (!AddressedId.TryParse(command.ItemId, out var itemId))
                {
                    throw new ConflictException(BoardErrorCodes.ReorderStale);
                }

                var moved = StackRank.Move(order, itemId, position);

                // 5. Every item at its place, the places of items that left dropped.
                await backlogs.SaveOrderAsync(scope, moved, ct);

                return true;
            },
            cancellationToken);
    }
}
