using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// The write side of team memberships (mod-016 plan-backend § 3.4). The team row
/// is locked through <see cref="ITeamRepository"/> (<c>FOR SHARE</c> by an add,
/// <c>FOR UPDATE</c> by the team's deletion), not here.
/// </summary>
public interface ITeamMembershipRepository
{
    /// <summary>Whether the user is in the team. Tracks nothing, takes no lock.</summary>
    Task<bool> ExistsAsync(Guid teamId, string userId, CancellationToken cancellationToken);

    void Add(TeamMembership membership);

    /// <summary>Deletes the user's membership of the team with one statement; answers whether a row was deleted.</summary>
    Task<bool> DeleteAsync(Guid teamId, string userId, CancellationToken cancellationToken);

    /// <summary>Ends every membership of the team, with one statement (BR-M1-18, BR-M1-36).</summary>
    Task EndAllAsync(Guid teamId, CancellationToken cancellationToken);

    /// <summary>
    /// Flushes the request's context; a lost add race on the primary key is <c>team.member_exists</c>, and a violation of
    /// the key onto the organization membership is <c>team.member_not_in_organization</c> (BR-M1-10).
    /// </summary>
    Task SaveAsync(CancellationToken cancellationToken);
}
