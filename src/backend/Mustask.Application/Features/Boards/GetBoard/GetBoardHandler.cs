using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Boards;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Boards.GetBoard;

/// <summary>
/// A team's board of one level (mod-006 US-4, feat-040 US-1, US-2, US-4): the backlog's items (the same
/// membership read, mod-017 BR-OT-5 — the items the team owns, in every active project of the organization) in the
/// team's order (BR-TS-6), each in the column its type's current state maps to, the columns derived from the
/// organization's process on this read (BR-M4-2, <see cref="BoardLayout"/>); each card carries its «Проект» and its checklist
/// of task, bug and test-case children (feat-032), whatever their team and whatever the project filter.
/// </summary>
/// <remarks>
/// Not found from the outside in, exactly as the backlog's read (api-contract § 0.3); the team is read again after the reads
/// (tech-011 m2). Reads only, no transaction.
/// </remarks>
public sealed class GetBoardHandler(
    IOrganizationStructure structure,
    IProcessDefinitionReadModel processes,
    IBacklogReadModel backlogs,
    IOrganizationAccess access,
    ICurrentUser currentUser)
{
    public async Task<BoardResponse> Handle(GetBoardQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(query.TeamId, out var teamId)
            || await structure.FindTeamAsync(organizationId, teamId, cancellationToken) is not { IsReachable: true } team)
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

        // The team's linked active projects (the filter's options), never narrowed by the filter.
        var linked = await structure.ListLinkedProjectsAsync(organizationId, teamId, cancellationToken);

        // The project filter (BR-TS-8) narrows the columns and their cards; a card's checklist below is not narrowed.
        var filter = WorkItemFilter.OfProjects(query.ProjectIds);
        var caller = new CallerAccess(organizationId, currentUser.SubjectId, access.Role);

        var rows = await backlogs.ReadTeamAsync(TeamBacklogScope.Of(teamId, resolved.Id, types, filter), caller, cancellationToken);

        // Every item of the backlog has a state outside Removed of one of the level's types, so it has exactly one column.
        var items = BacklogItemView.InOrder(rows);

        // feat-032 BR-C-10: every card's checklist comes with the board, in one more read for all the cards at once.
        var checklists = await backlogs.ReadChecklistsAsync(organizationId, items.Select(item => item.Id).ToList(), cancellationToken);
        var cards = items
            .Select(item => BoardCardView.Of(item, checklists))
            .ToLookup(card => layout.ColumnOf(card.Type.Id, card.State.Id));

        // tech-011 m2: a team deleted while the reads ran is not an empty board but its not-found; read once more, last.
        if (await structure.FindTeamAsync(organizationId, teamId, cancellationToken) is not { IsReachable: true })
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
        }

        var columns = layout.Columns
            .Select((column, index) => new BoardColumnView(
                column.Key,
                column.Header,
                new WorkItemStateCategoryView(column.Category.ToString(), StateCategoryLabels.Of(column.Category)),
                column.Targets.Select(target => new BoardColumnTargetView(target.TypeId, target.StateId)).ToList(),
                cards[index].ToList()))
            .ToList();

        return new BoardResponse(
            new BoardTeamView(team.Id, team.Name),
            linked.Select(project => new BoardProjectView(project.Id, project.Name)).ToList(),
            BoardLevelView.Of(level, processLevel, types),
            columns);
    }
}
