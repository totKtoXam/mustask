using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Domain.Boards;
using Mustask.Domain.Process;

namespace Mustask.Application.Abstractions.Boards;

/// <summary>
/// The read side of a team's backlog and board (mod-006). Both surfaces read the
/// same items through the one membership query (spec § Model, conditions 1–4),
/// so a board never holds what the backlog does not.
/// </summary>
public interface IBacklogReadModel
{
    /// <summary>
    /// The backlog's items with what a row and a card show and their place on
    /// this backlog, in no particular order (the order is the domain's
    /// <see cref="StackRank"/>), and the team's default area as it stands now.
    /// A fixed number of queries.
    /// </summary>
    Task<BacklogRead> ReadAsync(BacklogScope scope, CancellationToken cancellationToken);
}

/// <summary>
/// One backlog (team, level) of a project, and what its membership filters on:
/// the level's types (condition 2) and their states outside the Removed category
/// (condition 4). Condition 3, the team's areas, is resolved by the reader from
/// the project's area tree at the moment of the read.
/// </summary>
public sealed record BacklogScope(
    Guid ProjectId,
    Guid TeamId,
    Guid LevelId,
    IReadOnlyCollection<Guid> TypeIds,
    IReadOnlyCollection<Guid> ShownStateIds)
{
    /// <summary>The scope of <paramref name="levelId"/>'s backlog of the team, from the organization's process.</summary>
    public static BacklogScope Of(Guid projectId, Guid teamId, Guid levelId, IEnumerable<WorkItemTypeRow> levelTypes)
    {
        var types = levelTypes.ToList();

        return new BacklogScope(
            projectId,
            teamId,
            levelId,
            types.Select(type => type.Id).ToList(),
            types.SelectMany(type => type.States).Where(state => state.Category != StateCategory.Removed).Select(state => state.Id).ToList());
    }
}

/// <summary>What <see cref="IBacklogReadModel.ReadAsync"/> answers.</summary>
public sealed record BacklogRead(WorkItemAreaView DefaultArea, IReadOnlyList<BacklogItemRow> Items);

/// <summary>An item of the backlog: mod-003's fields a row and a card show, and its place here, <c>null</c> when unplaced.</summary>
public sealed record BacklogItemRow(
    Guid Id,
    string Identifier,
    int Number,
    int? Place,
    WorkItemTypeView Type,
    string Title,
    WorkItemAssigneeView? Assignee,
    WorkItemStateView State);
