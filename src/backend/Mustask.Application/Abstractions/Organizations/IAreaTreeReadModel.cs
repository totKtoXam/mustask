using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// The organization's area tree and the links of areas, as reads (mod-018): a fixed number of statements whatever the number of areas,
/// their depth or their links (spec § NFR Query shape). The handlers assemble pre-order and paths through <see cref="AreaTree"/>.
/// Every read carries the organization, so another organization's rows are never read (BR-AW-1).
/// </summary>
public interface IAreaTreeReadModel
{
    /// <summary>One statement: every area of the organization, siblings by name in Russian collation (case not deciding), then id.</summary>
    Task<IReadOnlyList<AreaTreeRow>> GetRowsAsync(Guid organizationId, CancellationToken cancellationToken);

    /// <summary>The <b>active</b> projects linked to the area, by name then id. A deactivated project's link stays stored and is left out (BR-AW-10).</summary>
    Task<IReadOnlyList<AreaProjectRow>> ListProjectsAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken);

    /// <summary>Every project linked to the area, deactivated ones too, with whether it is active: the deletion's confirmation (BR-AW-9).</summary>
    Task<IReadOnlyList<AreaProjectRow>> ListAllProjectsAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken);

    /// <summary>The active teams linked to the area, by name then id.</summary>
    Task<IReadOnlyList<AreaTeamRow>> ListTeamsAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken);

    /// <summary>The ids of the areas linked to the project, whatever its status (the caller judged the project).</summary>
    Task<IReadOnlyList<Guid>> ListAreaIdsOfProjectAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken);

    /// <summary>The ids of the areas linked to the team.</summary>
    Task<IReadOnlyList<Guid>> ListAreaIdsOfTeamAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken);
}

/// <summary>One area: its place, its name and its description.</summary>
public sealed record AreaTreeRow(Guid Id, Guid? ParentId, string Name, string? Description)
{
    /// <summary>The row as <see cref="AreaTree"/> takes it.</summary>
    public AreaNode ToNode() => new(Id, ParentId, Name);
}

/// <summary>A project linked to an area.</summary>
public sealed record AreaProjectRow(Guid Id, string Name, string Key, bool IsActive);

/// <summary>A team linked to an area.</summary>
public sealed record AreaTeamRow(Guid Id, string Name);
