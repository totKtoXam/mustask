using Mustask.Domain.Organizations;
using Mustask.Domain.Process;

namespace Mustask.Application.Features.Organizations;

/// <summary>
/// The in-process reads M1 offers other modules (api-contract § 7). It sits at
/// the module root, not inside a slice, so a slice of another module can depend
/// on it without referencing a slice of this one (docs/conventions/backend.md:
/// modules talk through explicit interfaces).
/// </summary>
/// <remarks>
/// <para><b>Registration:</b> scoped. Resolve it in the request's scope: it uses the request's database context.</para>
/// <para>
/// Every by-id read returns the object with its status, reachable or not
/// (ADR-0026 rule 4); <c>null</c> means only that no such object ever existed.
/// None of them checks access, and nothing here publishes an event (ADR-0026
/// rule 7): a module that must act on a removal reads the status in its own
/// write path.
/// </para>
/// </remarks>
public interface IOrganizationStructure
{
    /// <summary>The organization with its status, process and creator (mod-016: the creator becomes the first administrator, BR-M1-21).</summary>
    Task<OrganizationInfo?> FindOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);

    /// <summary>The project with its status, reachability, first team, root area and creator (mod-003: the area with no team context, BR-M1-14).</summary>
    Task<ProjectInfo?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// The team with its status. A deleted team answers <see cref="TeamStatus.Deleted"/>
    /// with no default area, never <c>null</c> and never active (US-7, doc-010 PR-9).
    /// </summary>
    Task<TeamInfo?> FindTeamAsync(Guid teamId, CancellationToken cancellationToken);

    /// <summary>The project's active teams: the first team first, then display order. A deleted team is left out.</summary>
    Task<IReadOnlyList<TeamInfo>> ListActiveTeamsAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// The team that owns the area, resolved exactly as the tree read resolves it
    /// (US-6), through <see cref="AreaTree"/>; <c>null</c> when the area does not
    /// exist (a deleted area is gone).
    /// </summary>
    Task<AreaOwnerInfo?> FindAreaOwnerAsync(Guid areaId, CancellationToken cancellationToken);
}

/// <summary>An organization for another module.</summary>
/// <param name="CreatedBy">The creator's Keycloak subject (<c>sub</c>).</param>
public sealed record OrganizationInfo(
    Guid Id,
    string Name,
    OrganizationStatus Status,
    ProcessId ProcessId,
    string CreatedBy,
    DateTimeOffset CreatedAt);

/// <summary>A project for another module.</summary>
/// <param name="IsReachable"><c>true</c> when the project and its organization are both active.</param>
/// <param name="RootAreaId">The area a work item created with no team context takes (BR-M1-14).</param>
public sealed record ProjectInfo(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string Key,
    ProjectStatus Status,
    bool IsReachable,
    Guid FirstTeamId,
    Guid RootAreaId,
    string CreatedBy,
    DateTimeOffset CreatedAt);

/// <summary>A team for another module.</summary>
/// <param name="DefaultAreaId">The team's default area; <c>null</c> for a deleted team.</param>
/// <param name="IsReachable"><c>true</c> when the team, its project and its organization are all active.</param>
public sealed record TeamInfo(
    Guid Id,
    Guid ProjectId,
    Guid OrganizationId,
    string Name,
    TeamStatus Status,
    bool IsFirstTeam,
    Guid? DefaultAreaId,
    bool IsReachable);

/// <summary>The owner of an area for another module.</summary>
/// <param name="IsInherited"><c>true</c> when the area has no explicit owner and inherits from an ancestor.</param>
/// <param name="IsReachable"><c>true</c> when the area's project and organization are both active.</param>
public sealed record AreaOwnerInfo(
    Guid AreaId,
    Guid ProjectId,
    Guid OwningTeamId,
    bool IsInherited,
    bool IsReachable);
