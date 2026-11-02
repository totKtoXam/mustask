using System.Linq.Expressions;

namespace Mustask.Domain.Organizations;

/// <summary>
/// ADR-0026 rule 3 as specifications: the decision of "reachable" lives here, in the domain, and is unit-testable without a database
/// (each expression compiles to a plain predicate). The persistence layer only applies them to a set and composes the container
/// checks that need a join; it decides nothing. Rule 4 holds: these are named predicates a read starts from, never a global query
/// filter. mod-018 moved them out of <c>Mustask.Infrastructure.Organizations.Reachability</c>.
/// </summary>
public static class ReachabilityRules
{
    /// <summary>An organization is active (the container of every project and team).</summary>
    public static Expression<Func<Organization, bool>> ActiveOrganization { get; } =
        organization => organization.Status == OrganizationStatus.Active;

    /// <summary>The organization's projects of one status (the active list and the deactivated list).</summary>
    public static Expression<Func<Project, bool>> ProjectOf(Guid organizationId, ProjectStatus status) =>
        project => project.OrganizationId == organizationId && project.Status == status;

    /// <summary>The organization's active teams.</summary>
    public static Expression<Func<Team, bool>> ActiveTeamOf(Guid organizationId) =>
        team => team.OrganizationId == organizationId && team.Status == TeamStatus.Active;

    /// <summary>An active team, wherever it is (a link's end, a default-team candidate).</summary>
    public static Expression<Func<Team, bool>> ActiveTeam { get; } = team => team.Status == TeamStatus.Active;

    /// <summary>The same decision in memory: a project is reachable when its own status and its organization's are active.</summary>
    public static bool IsReachable(OrganizationStatus organization, ProjectStatus project) =>
        organization == OrganizationStatus.Active && project == ProjectStatus.Active;

    /// <summary>A team is reachable when its own status and its organization's are active (feat-028 BR-OL-1).</summary>
    public static bool IsReachable(OrganizationStatus organization, TeamStatus team) =>
        organization == OrganizationStatus.Active && team == TeamStatus.Active;
}
