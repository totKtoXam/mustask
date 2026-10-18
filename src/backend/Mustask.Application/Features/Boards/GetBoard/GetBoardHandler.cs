using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Boards;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Boards.GetBoard;

/// <summary>
/// A team's board of one level (US-4): the backlog's items (the same
/// membership read, mod-017 BR-OT-5 — the items the team owns) in the backlog's order (BR-M4-15), each in the
/// column its type's current state maps to, the columns derived from the
/// organization's process on this read (BR-M4-2, <see cref="BoardLayout"/>); each
/// card carries its checklist of task, bug and test-case children (feat-032).
/// </summary>
/// <remarks>
/// Not found from the outside in, exactly as the backlog's read (api-contract
/// § 0.3). Reads only, no transaction.
/// </remarks>
public sealed class GetBoardHandler(
    IOrganizationStructure structure,
    IProcessDefinitionReadModel processes,
    IBacklogReadModel backlogs)
{
    public async Task<BoardResponse> Handle(GetBoardQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(query.ProjectId, out var projectId)
            || await structure.FindProjectAsync(organizationId, projectId, cancellationToken) is not { IsReachable: true } project)
        {
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }

        if (!AddressedId.TryParse(query.TeamId, out var teamId)
            || await structure.FindProjectTeamAsync(organizationId, projectId, teamId, cancellationToken) is not { IsLinked: true, Team.IsReachable: true } linked)
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
        }

        if (!BoardLevelNames.TryParse(query.Level, out var level)
            || await processes.FindByOrganizationAsync(organizationId, cancellationToken) is not { } process
            || level.Resolve(process.BacklogLevels.Select(row => new ProcessLevel(row.Id, row.Rank, row.Kind))) is not { } resolved)
        {
            throw new NotFoundException(BoardErrorCodes.LevelNotFound);
        }

        var processLevel = process.BacklogLevels.Single(row => row.Id == resolved.Id);
        var types = process.WorkItemTypes.Where(type => type.BacklogLevelId == resolved.Id).ToList();

        var layout = BoardLayout.Derive(types
            .Select(type => new LayoutType(
                type.Id,
                type.States.Select(state => new LayoutState(state.Id, state.Name, state.Category, state.Position)).ToList()))
            .ToList());

        var rows = await backlogs.ReadAsync(BacklogScope.Of(projectId, teamId, resolved.Id, types), cancellationToken);

        // Every item of the backlog has a state outside Removed of one of the level's types, so it has exactly one column.
        var items = BacklogItemView.InOrder(rows);

        // feat-032 BR-C-10: every card's checklist comes with the board, in one more read for all the cards at once.
        var checklists = await backlogs.ReadChecklistsAsync(projectId, items.Select(item => item.Id).ToList(), cancellationToken);
        var cards = items
            .Select(item => BoardCardView.Of(item, checklists))
            .ToLookup(card => layout.ColumnOf(card.Type.Id, card.State.Id));

        var columns = layout.Columns
            .Select((column, index) => new BoardColumnView(
                column.Key,
                column.Header,
                new WorkItemStateCategoryView(column.Category.ToString(), StateCategoryLabels.Of(column.Category)),
                column.Targets.Select(target => new BoardColumnTargetView(target.TypeId, target.StateId)).ToList(),
                cards[index].ToList()))
            .ToList();

        return new BoardResponse(
            new BoardTeamView(linked.Team.Id, linked.Team.Name),
            BoardLevelView.Of(level, processLevel, types),
            columns);
    }
}
