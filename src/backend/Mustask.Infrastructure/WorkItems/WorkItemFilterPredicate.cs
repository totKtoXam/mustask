using Mustask.Domain.WorkItems;
using Mustask.Infrastructure.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.WorkItems;

/// <summary>
/// The one predicate of the shared filter specification (mod-017 BR-OT-8, feat-040 BR-TS-8, feat-003 BR-WS-16, api-contract § 3):
/// the project's list, backlog and board narrow their items by the team dimension through it, a team's backlog and board by the
/// project dimension, and the project's list by the state, the assignee and the keyword, so the surfaces cannot disagree for any
/// combination. It is composed on <see cref="IQueryable"/> and goes into the same statement as the rest of the query — a filter
/// adds none. Boards' memberships use it too: Boards' infrastructure already uses WorkItems' (<see cref="WorkItemRefs"/>). The
/// dimensions combine with AND; the values of one dimension with OR (BR-WS-2). The predicate decides <b>which of the given items</b>
/// match; which items it is given — live ones, or the deleted ones as well — is the entry point's (<see cref="WorkItemSets"/>).
/// </summary>
internal static class WorkItemFilterPredicate
{
    /// <summary>
    /// <paramref name="items"/> narrowed by <paramref name="filter"/>: an item matches when it is owned by any chosen team (or by
    /// none when <c>none</c> is chosen), when (a team's views) its project is any chosen project, its state bears any chosen
    /// name, its assignee is any chosen user (or it has none when <c>none</c> is chosen) and the keyword is in its title, or is
    /// its identifier or its number. No filter leaves the items as they are; a dimension that is set but names nothing the item
    /// can have selects nothing (a malformed value, an unknown, deleted, unlinked, other-project or foreign id, a name no state
    /// bears).
    /// </summary>
    public static IQueryable<WorkItem> Apply(MustaskDbContext context, IQueryable<WorkItem> items, WorkItemFilter filter)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.TeamsSet)
        {
            var teamIds = filter.TeamIds.ToList();
            var noTeam = filter.NoTeam;

            items = items.Where(item =>
                (item.OwningTeamId != null && teamIds.Contains(item.OwningTeamId.Value))
                || (noTeam && item.OwningTeamId == null));
        }

        if (filter.ProjectsSet)
        {
            var projectIds = filter.ProjectIds.ToList();

            items = items.Where(item => projectIds.Contains(item.ProjectId));
        }

        if (filter.StatesSet)
        {
            // A state name has one category on every type of a process (StateNameRule), so a name alone says what the item is.
            var names = filter.StateNames.ToList();

            items = items.Where(item => context.StateDefinitions.Any(state => state.Id == item.StateId && names.Contains(state.Name)));
        }

        if (filter.AssigneesSet)
        {
            // Ids are compared as stored: nothing is looked up in the directory, so a former member's items are found by id.
            var assigneeIds = filter.AssigneeIds.ToList();
            var noAssignee = filter.NoAssignee;

            items = items.Where(item =>
                (item.AssigneeId != null && assigneeIds.Contains(item.AssigneeId))
                || (noAssignee && item.AssigneeId == null));
        }

        return filter.Keyword is { } keyword ? Matching(context, items, keyword) : items;
    }

    /// <summary>
    /// The keyword's three readings (BR-WS-9) in one predicate, so an item matching more than one is one row: (a) the title holds
    /// the text, letter case and «ё» ignored — both sides lower-cased by the database in the parent picker's collation, then «ё»
    /// replaced by «е»; <c>strpos</c> has no wildcards, so <c>%</c>, <c>_</c> and <c>\</c> are literal and nothing is escaped;
    /// (b) the text is the item's identifier — its number and its type's key, the project's key being the addressed one (the caller
    /// has narrowed the items to the project); (c) the text is the item's number.
    /// </summary>
    private static IQueryable<WorkItem> Matching(MustaskDbContext context, IQueryable<WorkItem> items, WorkItemKeyword keyword)
    {
        if (keyword.MatchesNothing)
        {
            return items.Where(_ => false);
        }

        var text = keyword.Text;
        var hasNumber = keyword.Number is not null;
        var number = keyword.Number ?? 0;
        var hasIdentifier = keyword.Identifier is not null;
        var identifierNumber = keyword.Identifier?.Number ?? 0;
        var typeKey = keyword.Identifier?.TypeKey ?? string.Empty;

        return items.Where(item =>
            EF.Functions.Collate(item.Title, OrganizationsCollation.Russian).ToLower().Replace("ё", "е")
                .Contains(EF.Functions.Collate(text, OrganizationsCollation.Russian).ToLower().Replace("ё", "е"))
            || (hasNumber && item.Number == number)
            || (hasIdentifier && item.Number == identifierNumber
                && context.WorkItemTypes.Any(type => type.Id == item.TypeId && type.Key == typeKey)));
    }
}
