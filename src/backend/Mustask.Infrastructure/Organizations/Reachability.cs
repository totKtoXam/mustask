using Mustask.Domain.Organizations;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// ADR-0026's "one place per module" for reachability: an object is reachable
/// when its own status is active and every container above it is active (rule
/// 3). Every read model, every <c>IsReachableAsync</c> and the seam compose
/// these, and no handler re-states them. Each is a <c>Where</c> that EF
/// translates to a join or an <c>EXISTS</c>, so it filters before
/// materialization. There is no global query filter (rule 4).
/// </summary>
internal static class Reachability
{
    public static IQueryable<Organization> ActiveOrganizations(this MustaskDbContext context) =>
        context.Organizations.Where(organization => organization.Status == OrganizationStatus.Active);

    /// <summary>The projects of <paramref name="status"/> in an active organization: the active list and the deactivated list.</summary>
    public static IQueryable<Project> ProjectsOfActiveOrganization(this MustaskDbContext context, Guid organizationId, ProjectStatus status)
    {
        var organization = context.ActiveOrganizations().Where(candidate => candidate.Id == organizationId);

        return context.Projects.Where(project =>
            project.OrganizationId == organizationId
            && project.Status == status
            && organization.Any());
    }

    /// <summary>Active projects of an active organization.</summary>
    public static IQueryable<Project> ReachableProjects(this MustaskDbContext context, Guid organizationId) =>
        context.ProjectsOfActiveOrganization(organizationId, ProjectStatus.Active);

    /// <summary>Deactivated projects of an active organization (US-3's deactivated list).</summary>
    public static IQueryable<Project> DeactivatedProjectsOf(this MustaskDbContext context, Guid organizationId) =>
        context.ProjectsOfActiveOrganization(organizationId, ProjectStatus.Deactivated);

    /// <summary>Active teams of a reachable project.</summary>
    public static IQueryable<Team> ReachableTeams(this MustaskDbContext context, Guid organizationId, Guid projectId)
    {
        var project = context.ReachableProjects(organizationId).Where(candidate => candidate.Id == projectId);

        return context.Teams.Where(team =>
            team.ProjectId == projectId
            && team.Status == TeamStatus.Active
            && project.Any());
    }

    /// <summary>Every area of a reachable project. Areas have no status of their own.</summary>
    public static IQueryable<Area> AreasOfReachableProject(this MustaskDbContext context, Guid organizationId, Guid projectId)
    {
        var project = context.ReachableProjects(organizationId).Where(candidate => candidate.Id == projectId);

        return context.Areas.Where(area => area.ProjectId == projectId && project.Any());
    }
}
