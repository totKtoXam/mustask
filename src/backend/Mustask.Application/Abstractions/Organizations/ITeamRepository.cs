using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// The write side of teams. The lock methods take the row in the mode a use case needs (plan-backend § 7, § 8.2): a rename
/// <c>FOR NO KEY UPDATE</c>, every write that binds to a team (a work item's owner, a membership, the project's default
/// setting) <c>FOR SHARE</c>, then re-checks that the team is active (ADR-0026 rule 8, ADR-0028 rule 8), and a deletion takes
/// <b>all</b> the project's active teams <c>FOR NO KEY UPDATE</c> (<see cref="LockProjectTeamsAsync"/>). The first two return the
/// team of any status in that project, or <c>null</c>.
/// </summary>
public interface ITeamRepository
{
    Task<Team?> LockForNoKeyUpdateAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken);

    Task<Team?> LockForShareAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken);

    /// <summary>
    /// mod-017 (plan-backend D-3, D-4, ADR-0028 rule 8): the project's <b>active</b> teams, locked <c>FOR NO KEY UPDATE</c>
    /// in id order and tracked — the team deletion's lock. <c>NO KEY UPDATE</c> and not <c>UPDATE</c>: a deletion writes no key
    /// of the team row (the only unique index on its status is partial), and a child created beneath an item of the team
    /// inserts with the owner key, a key share on the team — which <c>FOR UPDATE</c> would make the child wait for, while the
    /// deletion waits for the parent the creation holds (a deadlock). It still conflicts with every binder's <c>FOR SHARE</c>
    /// and with another deletion, and locking all the teams in one order serializes two deletions that would empty a project
    /// (BR-OT-13 (4)). The count of the list is the count the last-team guard reads.
    /// </summary>
    Task<IReadOnlyList<Team>> LockProjectTeamsAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// The team's default mark as the list shows it (mod-017 BR-OT-4): <see cref="DefaultTeamMark.Named"/> when the project's
    /// settings name it, <see cref="DefaultTeamMark.OnlyTeam"/> when it is the project's only active team, else none. One
    /// statement; for the answer of a rename, which holds the team's lock and names no other row.
    /// </summary>
    Task<DefaultTeamMark> GetMarkAsync(Team team, CancellationToken cancellationToken);

    /// <summary>Whether an active team of the project, other than <paramref name="exceptId"/>, holds the normalized name.</summary>
    Task<bool> IsActiveNameTakenAsync(Guid projectId, string normalizedName, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>The team's name, any status; <c>null</c> when there is none. For a refusal that names a team.</summary>
    Task<string?> FindNameAsync(Guid teamId, CancellationToken cancellationToken);

    /// <summary>mod-017: adds an ordinary team, with no area (<see cref="Team.Create"/>).</summary>
    void Add(Team team);

    /// <inheritdoc cref="IOrganizationRepository.SaveAsync"/>
    Task SaveAsync(CancellationToken cancellationToken);
}
