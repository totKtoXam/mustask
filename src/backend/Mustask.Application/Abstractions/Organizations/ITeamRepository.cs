using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// The write side of teams. The three lock methods take the row in the mode a
/// use case needs (plan-backend § 8.2): the deletion <c>FOR UPDATE</c>, a rename
/// or a default change <c>FOR NO KEY UPDATE</c>, and every write that binds to a
/// team (an area's owner) <c>FOR SHARE</c>, then re-checks that the team is
/// active (ADR-0026 rule 8). Each returns the team of any status in that
/// project, or <c>null</c>.
/// </summary>
public interface ITeamRepository
{
    Task<Team?> LockForUpdateAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken);

    Task<Team?> LockForNoKeyUpdateAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken);

    Task<Team?> LockForShareAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken);

    /// <summary>The project's first team, tracked, with no lock: it is never deleted (BR-M1-18).</summary>
    Task<Team> GetFirstTeamAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>Whether an active team of the project, other than <paramref name="exceptId"/>, holds the normalized name.</summary>
    Task<bool> IsActiveNameTakenAsync(Guid projectId, string normalizedName, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>The team's name, any status; <c>null</c> when there is none. For a refusal that names a team.</summary>
    Task<string?> FindNameAsync(Guid teamId, CancellationToken cancellationToken);

    /// <summary>
    /// One set-based statement: every area <paramref name="fromTeamId"/> owns
    /// explicitly passes to <paramref name="toTeamId"/>, and its default mark is
    /// cleared (BR-M1-18). Under READ COMMITTED it waits on any area another
    /// transaction holds and re-evaluates its condition on the committed row.
    /// </summary>
    Task ReassignExplicitAreasAsync(Guid fromTeamId, Guid toTeamId, CancellationToken cancellationToken);

    /// <summary>Adds the team with its default area.</summary>
    void Add(NewTeam team);

    /// <inheritdoc cref="IOrganizationRepository.SaveAsync"/>
    Task SaveAsync(CancellationToken cancellationToken);
}
