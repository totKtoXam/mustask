using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// The locks on <c>project_teams</c> rows that <b>return the rows they lock</b> (feat-028 review-security S-1, review-code CR-4). Under
/// READ COMMITTED every statement has a snapshot of its own, so a lock statement followed by a second read of the same rows judges the
/// rows of two different moments: a link committed between the two reads as linked, though the lock never held it, and the keys of an
/// item or a setting that binds to it then take a key share the unlink's or the deletion's <c>FOR NO KEY UPDATE</c> admits — its final
/// <c>DELETE</c> waits for the binder and raises <c>23001</c> (RESTRICT) when the binder commits. A <c>SELECT … FOR …</c> returns
/// exactly the rows it locked, so the caller decides "linked" from this answer, and from nothing else. Every statement carries the
/// organization (ADR-0028 § 4), so an id of another organization locks, and returns, nothing.
/// </summary>
/// <remarks>
/// The rows come back through <c>SqlQuery&lt;Guid&gt;</c>, which EF composes as <c>SELECT … FROM (&lt;statement&gt;) AS t</c> and runs
/// to its end (<c>ToListAsync</c> has no <c>LIMIT</c>), so the <c>ORDER BY</c> inside is applied before the locks are taken and
/// every row is locked — the same shape <c>BacklogRepository.ReadEntriesAsync</c> uses for its locking read.
/// </remarks>
internal static class ProjectTeamLocks
{
    /// <summary>
    /// Locks the link of one team to the project <c>FOR SHARE</c> — an item binder's lock — and answers whether the statement found
    /// it: <c>true</c> means this transaction holds the link until it ends, <c>false</c> that the pair was not linked at the
    /// statement (an unlink or a deletion that committed first, a link not yet committed, a team of another organization).
    /// </summary>
    public static async Task<bool> ShareAsync(
        MustaskDbContext context, Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken)
    {
        var held = await context.Database
            .SqlQuery<Guid>(
                $"""
                 SELECT team_id AS "Value"
                 FROM project_teams
                 WHERE organization_id = {organizationId} AND project_id = {projectId} AND team_id = {teamId}
                 FOR SHARE
                 """)
            .ToListAsync(cancellationToken);

        return held.Count > 0;
    }

    /// <summary>
    /// Locks every link of the project <c>FOR SHARE</c> in one statement ordered by team id — the order an unlink (the pair, by team
    /// id) and a deletion (the team's links, by project id) take them in — and answers the teams whose links it holds. A link
    /// committed after the statement is not among them and is not held: the caller must not bind to it.
    /// </summary>
    public static async Task<IReadOnlySet<Guid>> ShareAllOfProjectAsync(
        MustaskDbContext context, Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        var held = await context.Database
            .SqlQuery<Guid>(
                $"""
                 SELECT team_id AS "Value"
                 FROM project_teams
                 WHERE organization_id = {organizationId} AND project_id = {projectId}
                 ORDER BY team_id
                 FOR SHARE
                 """)
            .ToListAsync(cancellationToken);

        return held.ToHashSet();
    }

    /// <summary>
    /// Locks the links of the project to <paramref name="teamIds"/> <c>FOR NO KEY UPDATE</c> in one statement ordered by team id — the
    /// unlink's lock — and answers the teams whose links it holds. A team that is not in the answer was not linked at the statement.
    /// </summary>
    public static async Task<IReadOnlySet<Guid>> NoKeyUpdateAsync(
        MustaskDbContext context, Guid organizationId, Guid projectId, Guid[] teamIds, CancellationToken cancellationToken)
    {
        var held = await context.Database
            .SqlQuery<Guid>(
                $"""
                 SELECT team_id AS "Value"
                 FROM project_teams
                 WHERE organization_id = {organizationId} AND project_id = {projectId} AND team_id = ANY({teamIds})
                 ORDER BY team_id
                 FOR NO KEY UPDATE
                 """)
            .ToListAsync(cancellationToken);

        return held.ToHashSet();
    }
}
