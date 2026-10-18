using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Boards;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Boards.ReorderProjectBacklog;

/// <summary>
/// Moves one item of a project's backlog to the top, to the bottom, or directly after another item (feat-039 US-2,
/// BR-PB-4), giving every item of the backlog the place it then stands at in the project's own order and dropping the
/// places of items that have left it. A reorder changes no field of any work item and no team's place.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.4): the address from the outside in, as the project backlog's read;
/// the position (400); then, under the locks, the project again and the stale check (409).
/// </para>
/// <para>
/// <b>Locks</b> (ADR-0011, ADR-0028 rules 8 and 9, mod-005 ADR-0030 rule 6): the project <c>FOR SHARE</c> — a write that
/// binds to a project locks it and re-checks it is reachable, so a project deactivated meanwhile answers not found and a
/// deactivation waits for the reorder — then the backlog's <b>members</b> <c>FOR KEY SHARE</c> in id order, as the team
/// reorder takes them, then the project backlog's order row <c>FOR UPDATE</c>, and the members read again in a locking
/// statement. Reorders of one project backlog therefore run one after another in arrival order, each reading the order as
/// the previous one committed it; a Delete or a purge of a member either commits first (the reorder's locking read no longer
/// finds the item) or waits for the reorder, so the place's key onto the item cannot fail. The team backlogs' lock rows and
/// places are other rows of other tables, so none of them waits for or fails a project reorder.
/// </para>
/// </remarks>
public sealed class ReorderProjectBacklogHandler(
    IOrganizationStructure structure,
    IProcessDefinitionReadModel processes,
    IProjectBacklogRepository backlogs,
    IUnitOfWork unitOfWork)
{
    public async Task Handle(ReorderProjectBacklogCommand command, CancellationToken cancellationToken)
    {
        // 1. The address, from the outside in.
        if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(command.ProjectId, out var projectId)
            || await structure.FindProjectAsync(organizationId, projectId, cancellationToken) is not { IsReachable: true } project)
        {
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
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

        var scope = ProjectBacklogScope.Of(projectId, resolved.Id, process.WorkItemTypes.Where(type => type.BacklogLevelId == resolved.Id));

        _ = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 3. The locks, then the order as the previous reorder committed it.
                if (!await backlogs.LockAsync(organizationId, scope, ct))
                {
                    throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
                }

                var order = StackRank.Order(await backlogs.ReadEntriesAsync(scope, ct));

                // 4. The move: a malformed or unknown item names nothing on this backlog, which is stale (mod-006 BR-M4-26).
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
