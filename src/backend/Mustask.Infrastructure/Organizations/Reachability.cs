using Mustask.Domain.Organizations;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// ADR-0026's "one place per module" for reachability: an object is reachable
/// when its own status is active and every container above it is active (rule
/// 3). The decision is the domain's (<see cref="ReachabilityRules"/>);
/// this class only applies it to the context's sets and joins the container checks. Every read model, every <c>IsReachableAsync</c> and the seam compose
/// these, and no handler re-states them. Each is a <c>Where</c> that EF
/// translates to a join or an <c>EXISTS</c>, so it filters before
/// materialization. There is no global query filter (rule 4).
/// </summary>
/// <remarks>
/// feat-028: a team belongs to its organization and to no project, so a team is reachable by its own status and its
/// organization's alone; a <i>link</i> is read through a project that is reachable (for a project's list) or through a team
/// that is (for a team's list), and a deactivated project hides its links from a team's list without removing them.
/// </remarks>
internal static class Reachability
{
    public static IQueryable<Organization> ActiveOrganizations(this MustaskDbContext context) =>
        context.Organizations.Where(ReachabilityRules.ActiveOrganization);

    /// <summary>The projects of <paramref name="status"/> in an active organization: the active list and the deactivated list.</summary>
    public static IQueryable<Project> ProjectsOfActiveOrganization(this MustaskDbContext context, Guid organizationId, ProjectStatus status)
    {
        var organization = context.ActiveOrganizations().Where(candidate => candidate.Id == organizationId);

        return context.Projects
            .Where(ReachabilityRules.ProjectOf(organizationId, status))
            .Where(project => organization.Any());
    }

    /// <summary>Active projects of an active organization.</summary>
    public static IQueryable<Project> ReachableProjects(this MustaskDbContext context, Guid organizationId) =>
        context.ProjectsOfActiveOrganization(organizationId, ProjectStatus.Active);

    /// <summary>Deactivated projects of an active organization (US-3's deactivated list).</summary>
    public static IQueryable<Project> DeactivatedProjectsOf(this MustaskDbContext context, Guid organizationId) =>
        context.ProjectsOfActiveOrganization(organizationId, ProjectStatus.Deactivated);

    /// <summary>Active teams of an active organization (feat-028 BR-OL-1): the organization's list and a team's own pages.</summary>
    public static IQueryable<Team> ActiveTeamsOf(this MustaskDbContext context, Guid organizationId)
    {
        var organization = context.ActiveOrganizations().Where(candidate => candidate.Id == organizationId);

        return context.Teams
            .Where(ReachabilityRules.ActiveTeamOf(organizationId))
            .Where(team => organization.Any());
    }

    /// <summary>Active teams <b>linked</b> to a reachable project: a project's list and a team's views at the project's address (BR-OL-11).</summary>
    public static IQueryable<Team> LinkedTeams(this MustaskDbContext context, Guid organizationId, Guid projectId)
    {
        var project = context.ReachableProjects(organizationId).Where(candidate => candidate.Id == projectId);

        return context.Teams
            .Where(ReachabilityRules.ActiveTeamOf(organizationId))
            .Where(team => project.Any()
                && context.ProjectTeams.Any(link => link.ProjectId == projectId && link.TeamId == team.Id));
    }

    /// <summary>Reachable (active) projects a team is linked to, of an active organization: a team's projects.</summary>
    public static IQueryable<Project> LinkedProjects(this MustaskDbContext context, Guid organizationId, Guid teamId) =>
        context.ReachableProjects(organizationId).Where(project =>
            context.ProjectTeams.Any(link => link.TeamId == teamId && link.ProjectId == project.Id));
}
