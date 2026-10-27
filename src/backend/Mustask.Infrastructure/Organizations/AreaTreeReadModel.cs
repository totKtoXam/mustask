using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// The area tree and the links of areas, one statement per read whatever the number of areas (mod-018 NFR Query shape). Pre-order
/// and paths are assembled from these rows by the handler through <c>AreaTree</c>. Every statement carries the organization.
/// </summary>
internal sealed class AreaTreeReadModel(MustaskDbContext context) : IAreaTreeReadModel
{
    public async Task<IReadOnlyList<AreaTreeRow>> GetRowsAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await context.Areas
            .AsNoTracking()
            .Where(area => area.OrganizationId == organizationId)
            .OrderBy(area => EF.Functions.Collate(area.Name, OrganizationsCollation.Russian))
            .ThenBy(area => area.Id)
            .Select(area => new AreaTreeRow(area.Id, area.ParentId, area.Name, area.Description))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AreaProjectRow>> ListProjectsAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken) =>
        await (
                from link in context.AreaProjects.AsNoTracking()
                join project in context.ReachableProjects(organizationId) on link.ProjectId equals project.Id
                where link.OrganizationId == organizationId && link.AreaId == areaId
                orderby EF.Functions.Collate(project.Name, OrganizationsCollation.Russian), project.Id
                select new AreaProjectRow(project.Id, project.Name, project.Key, true))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AreaProjectRow>> ListAllProjectsAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken) =>
        await (
                from link in context.AreaProjects.AsNoTracking()
                join project in context.Projects on link.ProjectId equals project.Id
                where link.OrganizationId == organizationId && link.AreaId == areaId
                orderby EF.Functions.Collate(project.Name, OrganizationsCollation.Russian), project.Id
                select new AreaProjectRow(project.Id, project.Name, project.Key, project.Status == ProjectStatus.Active))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AreaTeamRow>> ListTeamsAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken) =>
        await (
                from link in context.AreaTeams.AsNoTracking()
                join team in context.ActiveTeamsOf(organizationId) on link.TeamId equals team.Id
                where link.OrganizationId == organizationId && link.AreaId == areaId
                orderby EF.Functions.Collate(team.Name, OrganizationsCollation.Russian), team.Id
                select new AreaTeamRow(team.Id, team.Name))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListAreaIdsOfProjectAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken) =>
        await context.AreaProjects
            .AsNoTracking()
            .Where(link => link.OrganizationId == organizationId && link.ProjectId == projectId)
            .Select(link => link.AreaId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListAreaIdsOfTeamAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) =>
        await context.AreaTeams
            .AsNoTracking()
            .Where(link => link.OrganizationId == organizationId && link.TeamId == teamId)
            .Select(link => link.AreaId)
            .ToListAsync(cancellationToken);
}
