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

    /// <summary>The project with its status, reachability, default team, root area and creator (mod-017 BR-OT-4: the default team, resolved in one place).</summary>
    Task<ProjectInfo?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// The team with its status. A deleted team answers <see cref="TeamStatus.Deleted"/>,
    /// never <c>null</c> and never active (US-7, doc-010 PR-9).
    /// </summary>
    Task<TeamInfo?> FindTeamAsync(Guid teamId, CancellationToken cancellationToken);

    /// <summary>
    /// The project's active teams by name in Russian collation (letter case not deciding), then by id: no team comes first
    /// (mod-017 BR-OT-11). A deleted team is left out.
    /// </summary>
    Task<IReadOnlyList<TeamInfo>> ListActiveTeamsAsync(Guid projectId, CancellationToken cancellationToken);
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
/// <param name="DefaultTeamId">
/// mod-017 (BR-OT-4): the project default team as it stands now — the team the project's settings name, else its only
/// active team, else <c>null</c> — resolved by <see cref="ProjectDefaultTeam"/> in the statement that reads the project.
/// </param>
/// <param name="RootAreaId">The area a new work item takes (a classification path until mod-018, mod-017 BR-OT-10).</param>
public sealed record ProjectInfo(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string Key,
    ProjectStatus Status,
    bool IsReachable,
    Guid? DefaultTeamId,
    Guid RootAreaId,
    string CreatedBy,
    DateTimeOffset CreatedAt);

/// <summary>A team for another module: a name and a status, no area and no first-team flag (mod-017 BR-OT-10, BR-OT-11).</summary>
/// <param name="IsReachable"><c>true</c> when the team, its project and its organization are all active.</param>
public sealed record TeamInfo(
    Guid Id,
    Guid ProjectId,
    Guid OrganizationId,
    string Name,
    TeamStatus Status,
    bool IsReachable);
