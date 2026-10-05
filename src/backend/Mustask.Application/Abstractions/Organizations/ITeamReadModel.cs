namespace Mustask.Application.Abstractions.Organizations;

/// <summary>Reads of teams through the reachability specification (ADR-0026 rules 3 and 4).</summary>
public interface ITeamReadModel
{
    /// <summary>
    /// The active teams of a reachable project: the first team first, then the rest
    /// in display order (api-contract § 3.1). Empty when the project is not reachable.
    /// </summary>
    Task<IReadOnlyList<TeamRow>> ListActiveAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken);

    /// <summary>The team when it is active in a reachable project; otherwise <c>null</c> (a deleted team included).</summary>
    Task<TeamRow?> FindReachableAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken);
}

/// <summary>One active team: its default area's id; the path comes from <see cref="Domain.Organizations.AreaTree"/>.</summary>
public sealed record TeamRow(Guid Id, Guid ProjectId, string Name, bool IsFirstTeam, Guid DefaultAreaId);
