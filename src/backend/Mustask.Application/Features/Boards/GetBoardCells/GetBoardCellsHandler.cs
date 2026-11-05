using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.Process;
using Mustask.Domain.Boards;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Boards.GetBoardCells;

/// <summary>
/// A page of one cell of the aggregate board (feat-048 BR-AV-7, BR-AV-9): the cards of a lane in a column, the project's rank
/// order in a by-project lane and change time otherwise, each with its checklist (feat-032) in one more statement. The column is
/// the key of the groups read (<c>InProgress-2</c>); a key the level's board does not have is a 400 naming <c>column</c>.
/// </summary>
public sealed class GetBoardCellsHandler(
    IAggregateViewReadModel views,
    IProcessDefinitionReadModel processes,
    IOrganizationAccess access,
    ICurrentUser currentUser)
{
    public async Task<BoardCellsResponse> Handle(GetBoardCellsQuery query, CancellationToken cancellationToken)
    {
        var organizationId = AggregateSupport.OrganizationOf(query.Request.OrganizationId);
        var parsed = AggregateParameters.Parse(query.Request, AggregateView.Boards, groupRead: true);
        var level = AggregateSupport.LevelOf(await processes.FindByOrganizationAsync(organizationId, cancellationToken), query.Request.Level);

        var column = level.Layout.Columns.FirstOrDefault(candidate => string.Equals(candidate.Key, query.Request.Column, StringComparison.Ordinal))
            ?? throw new ValidationException(
                BoardErrorCodes.ValidationFailed,
                new Dictionary<string, IReadOnlyList<FieldError>> { ["column"] = [new FieldError(BoardErrorCodes.InvalidParameter)] });

        var caller = new CallerAccess(organizationId, currentUser.SubjectId, access.Role);
        var stateIds = column.Targets.Select(target => target.StateId).OfType<Guid>().ToList();
        var scope = AggregateSupport.ScopeOf(AggregateView.Boards, level, parsed, stateIds);

        var page = await views.ReadGroupPageAsync(caller, scope, parsed.Group, parsed.After, parsed.Limit, cancellationToken);

        if (!page.ProjectFound)
        {
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }

        var checklists = await views.ReadChecklistsAsync(caller, page.Rows.Select(row => row.Row.Id).ToList(), cancellationToken);

        var cards = page.Rows
            .Select(row => new AggregateCardView(
                row.Row.Id, row.Row.Identifier, row.Row.Number, row.Row.Type, row.Row.Title, row.Row.Assignee, row.Row.State,
                row.Row.Priority, row.Row.Team, row.Row.Project, row.ChangedAt,
                checklists.TryGetValue(row.Row.Id, out var children) ? children : []))
            .ToList();

        return new BoardCellsResponse(cards, page.Next is null ? null : GroupCursor.Encode(page.Next, parsed.Fingerprint));
    }
}
