using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// The write side of teams (feat-028 plan-backend § 6, § 7). The lock methods take the team row in the mode a use case needs
/// and carry the organization, so a team of another organization locks nothing and answers <c>null</c>: a rename and a
/// description write <c>FOR NO KEY UPDATE</c>; every write that binds to a team (a link, a project created with teams, a member
/// add, a team reorder) <c>FOR SHARE</c>, then re-checks that the team is active (ADR-0026 rule 8, ADR-0028 rule 8). A deletion
/// takes the team <c>FOR NO KEY UPDATE</c> too and then the team's links (<see cref="IProjectTeamRepository"/>). The single-team
/// locks return the team of any status in that organization, or <c>null</c>.
/// </summary>
public interface ITeamRepository
{
    Task<Team?> LockForNoKeyUpdateAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken);

    Task<Team?> LockForShareAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken);

    /// <summary>
    /// The teams of the organization among <paramref name="teamIds"/>, locked <c>FOR SHARE</c> in <b>one statement ordered by
    /// id</b> (ADR-0028 rule 8: rows of one step in one order) and tracked, of any status. An id that names no team of the
    /// organization is simply absent from the answer; the caller compares the count and judges the status.
    /// </summary>
    Task<IReadOnlyList<Team>> LockManyForShareAsync(Guid organizationId, IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken);

    /// <summary>Whether an active team of the organization, other than <paramref name="exceptId"/>, holds the normalized name.</summary>
    Task<bool> IsActiveNameTakenAsync(Guid organizationId, string normalizedName, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>The team's name, any status; <c>null</c> when there is none. For a refusal that names a team.</summary>
    Task<string?> FindNameAsync(Guid teamId, CancellationToken cancellationToken);

    /// <summary>Adds a team of the organization (<see cref="Team.Create"/>).</summary>
    void Add(Team team);

    /// <inheritdoc cref="IOrganizationRepository.SaveAsync"/>
    Task SaveAsync(CancellationToken cancellationToken);
}
