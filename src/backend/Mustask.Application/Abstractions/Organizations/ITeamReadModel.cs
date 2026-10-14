namespace Mustask.Application.Abstractions.Organizations;

/// <summary>Reads of teams through the reachability specification (ADR-0026 rules 3 and 4).</summary>
public interface ITeamReadModel
{
    /// <summary>
    /// The active teams of a reachable project: the first team first, then the rest
    /// in display order (api-contract § 3.1). Empty when the project is not reachable.
    /// Each row says whether <paramref name="userId"/> is in that team (mod-016 BR-M1-27,
    /// D-5): read with the list, in the same query, so a change counts from the next read.
    /// </summary>
    Task<IReadOnlyList<TeamRow>> ListActiveAsync(Guid organizationId, Guid projectId, string userId, CancellationToken cancellationToken);

    /// <summary>
    /// The team when it is active in a reachable project; otherwise <c>null</c> (a deleted team included).
    /// The row's <see cref="TeamRow.IsMember"/> is always <c>false</c>: the single-team read does not carry it.
    /// </summary>
    Task<TeamRow?> FindReachableAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken);
}

/// <summary>
/// One active team: its default area's id; the path comes from <see cref="Domain.Organizations.AreaTree"/>.
/// <paramref name="IsMember"/> is the acting user's membership of the team, set only by the list (mod-016).
/// </summary>
public sealed record TeamRow(Guid Id, Guid ProjectId, string Name, bool IsFirstTeam, Guid DefaultAreaId, bool IsMember = false);
