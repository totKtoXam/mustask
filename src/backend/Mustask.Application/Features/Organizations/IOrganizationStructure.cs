using Mustask.Domain.Organizations;
using Mustask.Domain.Process;

namespace Mustask.Application.Features.Organizations;

/// <summary>
/// The in-process reads M1 offers other modules (api-contract § 7, feat-028 api-contract § 6). It sits at
/// the module root, not inside a slice, so a slice of another module can depend
/// on it without referencing a slice of this one (docs/conventions/backend.md:
/// modules talk through explicit interfaces).
/// </summary>
/// <remarks>
/// <para><b>Registration:</b> scoped. Resolve it in the request's scope: it uses the request's database context.</para>
/// <para>
/// Every by-id read returns the object with its status, reachable or not
/// (ADR-0026 rule 4); <c>null</c> means that no such object ever existed — <b>or that it belongs to another organization</b>:
/// every by-id read takes the organization and answers <c>null</c> for a foreign object (feat-028 BR-OL-1, mod-017 S-2), so no
/// caller compares organizations itself and none can forget to. None of them checks access, and nothing here publishes an
/// event (ADR-0026 rule 7): a module that must act on a removal reads the status in its own write path.
/// </para>
/// </remarks>
public interface IOrganizationStructure
{
    /// <summary>The organization with its status, process and creator (mod-016: the creator becomes the first administrator, BR-M1-21).</summary>
    Task<OrganizationInfo?> FindOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);

    /// <summary>
    /// The project of that organization with its status, reachability, default team, root area and creator (mod-017 BR-OT-4: the
    /// default team, resolved in one place over the project's <b>linked</b> teams). <c>null</c> for a project of another organization.
    /// </summary>
    Task<ProjectInfo?> FindProjectAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// The team of that organization with its status. A deleted team answers <see cref="TeamStatus.Deleted"/>,
    /// never <c>null</c> and never active (US-7, doc-010 PR-9). <c>null</c> for a team of another organization.
    /// </summary>
    Task<TeamInfo?> FindTeamAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken);

    /// <summary>
    /// The team of that organization and whether it is linked to the project, in one statement (feat-028 BR-OL-6). <c>null</c>
    /// when the team is unknown or of another organization. The link is read for the project as given: the caller has judged
    /// the project under the organization already.
    /// </summary>
    Task<ProjectTeamInfo?> FindProjectTeamAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken);

    /// <summary>
    /// The project's linked <b>active</b> teams by name in Russian collation (letter case not deciding), then by id: no team comes
    /// first (mod-017 BR-OT-11). A deleted team is left out. Empty for a project of another organization.
    /// </summary>
    Task<IReadOnlyList<TeamInfo>> ListLinkedTeamsAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// The projects the team is linked to that are <b>reachable</b>, by name then id. Empty for a team of another organization.
    /// </summary>
    Task<IReadOnlyList<LinkedProjectInfo>> ListLinkedProjectsAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken);
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
/// mod-017 (BR-OT-4), feat-028 (BR-OL-7): the project default team as it stands now — the team the project's settings name when
/// it is still linked and active, else its only linked active team, else <c>null</c> — resolved by <see cref="ProjectDefaultTeam"/>
/// in the statement that reads the project.
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

/// <summary>A team for another module: a name and a status, no project (feat-028 BR-OL-1), no area and no first-team flag.</summary>
/// <param name="IsReachable"><c>true</c> when the team and its organization are both active.</param>
public sealed record TeamInfo(
    Guid Id,
    Guid OrganizationId,
    string Name,
    TeamStatus Status,
    bool IsReachable);

/// <summary>A team with the one fact a project's write needs about it: whether the team is linked to the project (BR-OL-6).</summary>
public sealed record ProjectTeamInfo(TeamInfo Team, bool IsLinked);

/// <summary>A project a team is linked to, for another module.</summary>
/// <param name="IsReachable"><c>true</c> when the project and its organization are both active.</param>
public sealed record LinkedProjectInfo(Guid Id, string Name, string Key, bool IsReachable);
