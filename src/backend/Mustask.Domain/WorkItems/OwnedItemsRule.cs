using System.Linq.Expressions;

namespace Mustask.Domain.WorkItems;

/// <summary>
/// mod-017 BR-OT-12, feat-028 BR-OL-8/-9: the items a team owns across a set of projects are the ones an unlink or a team
/// deletion reassigns, in every state (a deleted item is owned until a release moves it, mod-005 BR-DR-9). The rule is one value:
/// the persistence layer counts by <see cref="Matches"/>, and renders the lock and the single set-based <c>UPDATE</c> from the
/// same <see cref="ProjectIds"/> and <see cref="TeamId"/>, so the count, the lock and the write cannot name different sets.
/// </summary>
public sealed class OwnedItemsRule
{
    private OwnedItemsRule(Guid[] projectIds, Guid teamId)
    {
        ProjectIds = projectIds;
        TeamId = teamId;
        Matches = item => item.OwningTeamId == teamId && projectIds.Contains(item.ProjectId);
    }

    /// <summary>The distinct projects the rule spans.</summary>
    public Guid[] ProjectIds { get; }

    /// <summary>The owning team.</summary>
    public Guid TeamId { get; }

    /// <summary>Whether the rule spans no project, so there is nothing to count, lock or write.</summary>
    public bool IsEmpty => ProjectIds.Length == 0;

    /// <summary>The items the team owns in those projects.</summary>
    public Expression<Func<WorkItem, bool>> Matches { get; }

    /// <summary>The team's owned items in <paramref name="projectIds"/> (duplicates collapse).</summary>
    public static OwnedItemsRule Of(IReadOnlyCollection<Guid> projectIds, Guid teamId) =>
        new([.. projectIds.Distinct()], teamId);
}
