using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Boards;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Boards.GetProjectBoard;

/// <summary>
/// A project's board of one level (feat-039 US-3): the backlog's items (the same membership read, BR-PB-2) in the
/// project's order (BR-PB-4), each in the column its type's current state maps to, the columns derived from the
/// organization's process on this read (BR-MX-19, <see cref="BoardLayout"/>); each card carries its checklist of
/// task, bug and test-case children (feat-032), whatever their team.
/// </summary>
/// <remarks>
/// Not found from the outside in, exactly as the project backlog's read (api-contract § 0.3). Reads only, no
/// transaction, a fixed number of statements.
/// </remarks>
public sealed class GetProjectBoardHandler(
    IOrganizationStructure structure,
    IProcessDefinitionReadModel processes,
    IBacklogReadModel backlogs)
{
    public async Task<ProjectBoardResponse> Handle(GetProjectBoardQuery query, CancellationToken cancellationToken)
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

        // The team filter (mod-017 BR-OT-8) narrows the columns and their cards; a card's checklist below is not narrowed.
        var filter = WorkItemFilter.OfTeams(query.TeamIds);

        var rows = await backlogs.ReadProjectAsync(ProjectBacklogScope.Of(projectId, resolved.Id, types, filter), cancellationToken);

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

        return new ProjectBoardResponse(
            new BoardProjectView(project.Id, project.Name),
            BoardLevelView.Of(level, processLevel, types),
            columns);
    }
}
