using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// The write side of project–team links (feat-028 BR-OL-4, plan-backend § 6, § 7). The link rows are step 3 of ADR-0028 rule 8,
/// taken in <b>(project id, team id)</b> order: an unlink locks the pair of one project by team id, a deletion locks one team's
/// links by project id, a creation that may default locks every link of one project by team id — each in one statement, so the
/// waits-for graph over links has no cycle. Every statement carries the organization, so a foreign id locks and changes nothing.
/// </summary>
public interface IProjectTeamRepository
{
    /// <summary>
    /// Locks the links of <paramref name="projectId"/> to <paramref name="teamIds"/> <c>FOR NO KEY UPDATE</c> in one statement
    /// ordered by team id — the unlink's lock (plan-backend D-3). <c>NO KEY UPDATE</c> conflicts with every binder's
    /// <c>FOR SHARE</c> and with another unlink or deletion, and not with a <c>FOR KEY SHARE</c>. An absent link locks nothing.
    /// Answers <b>the teams whose links the statement locked</b> — it returns the rows it holds — and the caller judges a team
    /// "linked" by this answer alone, never by a later read (review-security S-1): a link committed after the statement is held by
    /// nobody, and an item or a setting that bound to it meanwhile would fail the unlink's final <c>DELETE</c> on the owner key.
    /// </summary>
    Task<IReadOnlySet<Guid>> LockLinksAsync(
        Guid organizationId, Guid projectId, IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken);

    /// <summary>
    /// Locks one link <c>FOR SHARE</c> — an item binder's lock (a default naming; an item write has the same lock in
    /// <c>IWorkItemRepository</c>). Answers whether <b>the locking statement itself</b> found the link (it returns the row it
    /// locked): <c>true</c> means this transaction holds the link until it ends. An unlink or a deletion that committed first
    /// removed the row before the statement, and a link committed after the statement is not found — so the caller judges the
    /// team "linked" by this answer, never by a second read (review-security S-1).
    /// </summary>
    Task<bool> LockLinkForShareAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken);

    /// <summary>
    /// Locks every link of the team <c>FOR NO KEY UPDATE</c> in one statement ordered by project id — the deletion's lock — and
    /// answers the project ids the links name, as they stand once the locks are granted.
    /// </summary>
    Task<IReadOnlyList<Guid>> LockTeamLinksAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken);

    /// <summary>Which of <paramref name="teamIds"/> are linked to the project now. One statement; tracks nothing.</summary>
    Task<IReadOnlySet<Guid>> FindLinkedAsync(Guid organizationId, Guid projectId, IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken);

    /// <summary>
    /// Links every pair that is not linked yet, with one statement (<c>INSERT … SELECT unnest … ON CONFLICT DO NOTHING</c>): a
    /// pair already linked keeps who linked it and when, and two requests that link one pair leave one row (BR-OL-4). Answers the
    /// number of links made.
    /// </summary>
    Task<int> LinkAsync(
        Guid organizationId,
        IReadOnlyCollection<(Guid ProjectId, Guid TeamId)> pairs,
        string linkedBy,
        DateTimeOffset linkedAt,
        CancellationToken cancellationToken);

    /// <summary>Removes the link, with one statement. The caller has reassigned the items and cleared the setting first.</summary>
    Task RemoveLinkAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken);

    /// <summary>Removes every link of the team, with one statement (a team's deletion).</summary>
    Task RemoveTeamLinksAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken);

    /// <summary>Adds a link whose team or project is created in the same operation (a team created with projects, a project created with teams).</summary>
    void Add(ProjectTeam link);

    /// <inheritdoc cref="IOrganizationRepository.SaveAsync"/>
    Task SaveAsync(CancellationToken cancellationToken);
}
