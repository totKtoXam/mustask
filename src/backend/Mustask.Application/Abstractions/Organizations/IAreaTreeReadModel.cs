using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// The area tree of a reachable project, in two queries whatever its size (NFR
/// Volumes): the project check and the nodes. The handler assembles the
/// pre-order and the paths through <see cref="AreaTree"/>. An area has no owner and no default team
/// (mod-017 BR-OT-10), so no team is joined.
/// </summary>
public interface IAreaTreeReadModel
{
    /// <summary>
    /// The project's areas, siblings in display order; <c>null</c> when the project is not
    /// reachable in that organization.
    /// </summary>
    Task<IReadOnlyList<AreaTreeRow>?> GetNodesAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken);
}

/// <summary>One area: its place in the tree and its name.</summary>
public sealed record AreaTreeRow(Guid Id, Guid? ParentId, string Name)
{
    /// <summary>The row as <see cref="AreaTree"/> takes it.</summary>
    public AreaNode ToNode() => new(Id, ParentId, Name);
}
