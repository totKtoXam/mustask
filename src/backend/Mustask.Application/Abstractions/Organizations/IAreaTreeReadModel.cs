using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// The organization's typed area tree and the team links of departments, as reads (mod-018): a fixed number of statements whatever the
/// number of nodes, their depth or their links (spec § NFR Query shape). The handlers assemble pre-order and paths through
/// <see cref="AreaTree"/>. Every read carries the organization, so another organization's rows are never read (BR-AW-1).
/// </summary>
public interface IAreaTreeReadModel
{
    /// <summary>
    /// One statement: every node of the organization with its type and, for a project's node and its sub-areas, whether the project is
    /// active; siblings by name in Russian collation (case not deciding), then id.
    /// </summary>
    Task<IReadOnlyList<AreaTreeRow>> GetRowsAsync(Guid organizationId, CancellationToken cancellationToken);

    /// <summary>The active teams linked to the department, by name then id. Empty for any other node (OQ-8 A, OQ-11 A).</summary>
    Task<IReadOnlyList<AreaTeamRow>> ListTeamsAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken);

    /// <summary>The ids of the departments linked to the team.</summary>
    Task<IReadOnlyList<Guid>> ListAreaIdsOfTeamAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken);
}

/// <summary>One node: its place, name, description, type and, below a project's node, the project and whether it is active.</summary>
public sealed record AreaTreeRow(
    Guid Id,
    Guid? ParentId,
    string Name,
    string? Description,
    AreaType Type = AreaType.Area,
    Guid? ProjectId = null,
    bool? ProjectActive = null,
    string? ProjectKey = null)
{
    /// <summary>The row as <see cref="AreaTree"/> takes it.</summary>
    public AreaNode ToNode() => new(Id, ParentId, Name, Type, ProjectId);
}

/// <summary>A team linked to a department.</summary>
public sealed record AreaTeamRow(Guid Id, string Name);
