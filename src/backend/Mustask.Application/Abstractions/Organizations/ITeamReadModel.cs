using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>Reads of teams and their links through the reachability specification (ADR-0026 rules 3 and 4; feat-028).</summary>
public interface ITeamReadModel
{
    /// <summary>
    /// The organization's <b>active</b> teams, by name in Russian collation (letter case not deciding), then by id, each with the
    /// <b>active</b> projects it is linked to, by name then id (feat-028 api-contract § 1.2). Two statements whatever the number
    /// of teams or projects. Empty when the organization is not active.
    /// </summary>
    Task<IReadOnlyList<OrganizationTeamRow>> ListOrganizationTeamsAsync(Guid organizationId, CancellationToken cancellationToken);

    /// <summary>The team when it is an active team of an active organization; otherwise <c>null</c> (a deleted team included).</summary>
    Task<TeamRow?> FindAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken);

    /// <summary>
    /// The active teams <b>linked</b> to a reachable project, <b>by name</b> in Russian collation, letter case not deciding, then
    /// by id (feat-028 api-contract § 1.3): no team comes first. Empty when the project is not reachable. Each row says whether
    /// <paramref name="userId"/> is in that team (mod-016 BR-M1-27) and which default mark the team carries (BR-OL-7): read with the
    /// list, in the same statement, so a change counts from the next read.
    /// </summary>
    Task<IReadOnlyList<LinkedTeamRow>> ListLinkedAsync(Guid organizationId, Guid projectId, string userId, CancellationToken cancellationToken);

    /// <summary>
    /// The team when it is active and linked to a reachable project; otherwise <c>null</c> (a deleted, a foreign and an unlinked
    /// team alike, BR-OL-11). The row's <see cref="LinkedTeamRow.IsMember"/> is always <c>false</c>: the single-team read does not carry it.
    /// </summary>
    Task<LinkedTeamRow?> FindLinkedAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken);

    /// <summary>
    /// The team's <b>active</b> projects, by name in Russian collation then id (api-contract § 1.2): the same projects, in the
    /// same order, as a team's row of the organization's list. Empty when the organization is not active.
    /// </summary>
    Task<IReadOnlyList<TeamProjectRow>> ListProjectsOfTeamAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken);

    /// <summary>
    /// Every project the team is linked to, <b>of any status</b>, by name then id (feat-028 api-contract § 1.5): the deletion's
    /// confirmation names the deactivated ones too. One statement.
    /// </summary>
    Task<IReadOnlyList<TeamLinkRow>> ListLinksWithProjectsAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken);
}

/// <summary>One active team (feat-028 api-contract § 1.1): a name and a description, no project.</summary>
public sealed record TeamRow(Guid Id, string Name, string? Description);

/// <summary>One team of the organization's list with its active projects (api-contract § 1.2).</summary>
public sealed record OrganizationTeamRow(Guid Id, string Name, string? Description, IReadOnlyList<ProjectRef> Projects);

/// <summary>A project named in a team's row: its id and name.</summary>
public sealed record ProjectRef(Guid Id, string Name);

/// <summary>An active project of a team's page (api-contract § 1.2).</summary>
public sealed record TeamProjectRow(Guid Id, string Name, string Key);

/// <summary>
/// One team as a project's list shows it (feat-028 api-contract § 1.3): <paramref name="Mark"/> is the team's default mark,
/// computed with <c>ProjectDefaultTeam.MarkOf</c> in the statement. <paramref name="IsMember"/> is the acting user's membership
/// of the team, set only by the list (mod-016).
/// </summary>
public sealed record LinkedTeamRow(Guid Id, string Name, DefaultTeamMark Mark, bool IsMember = false);

/// <summary>A project a team is linked to, with its status folded to <paramref name="IsActive"/> (api-contract § 1.5).</summary>
public sealed record TeamLinkRow(Guid ProjectId, string Name, bool IsActive);

/// <summary>The wire names of the default mark (api-contract § 1.3): <c>named</c>, <c>onlyTeam</c>, and <c>null</c> for neither.</summary>
public static class DefaultTeamMarkNames
{
    public static string? ToWireName(this DefaultTeamMark mark) =>
        mark switch
        {
            DefaultTeamMark.Named => "named",
            DefaultTeamMark.OnlyTeam => "onlyTeam",
            _ => null,
        };
}
