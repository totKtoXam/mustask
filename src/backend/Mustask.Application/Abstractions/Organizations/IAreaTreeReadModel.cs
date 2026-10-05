using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// The area tree of a reachable project, in two queries whatever its size (NFR
/// Volumes): the project check and the nodes. The handler assembles the
/// pre-order, the paths and the resolved owners through <see cref="AreaTree"/>.
/// </summary>
public interface IAreaTreeReadModel
{
    /// <summary>
    /// The project's areas, siblings in display order, each with its explicit
    /// owner's and its default team's names; <c>null</c> when the project is not
    /// reachable in that organization.
    /// </summary>
    Task<IReadOnlyList<AreaTreeRow>?> GetNodesAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken);
}

/// <summary>One area with the names of the teams it refers to.</summary>
public sealed record AreaTreeRow(
    Guid Id,
    Guid? ParentId,
    string Name,
    Guid? OwnerTeamId,
    string? OwnerTeamName,
    Guid? DefaultOfTeamId,
    string? DefaultOfTeamName)
{
    /// <summary>The row as <see cref="AreaTree"/> takes it.</summary>
    public AreaNode ToNode() => new(Id, ParentId, Name, OwnerTeamId, DefaultOfTeamId);
}
