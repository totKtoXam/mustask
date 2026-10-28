using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// The typed area tree and the team links of departments, one statement per read whatever the number of nodes (mod-018 NFR Query
/// shape). Pre-order and paths are assembled from these rows by the handler through <c>AreaTree</c>. Every statement carries the
/// organization.
/// </summary>
internal sealed class AreaTreeReadModel(MustaskDbContext context) : IAreaTreeReadModel
{
    public async Task<IReadOnlyList<AreaTreeRow>> GetRowsAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await (
                from area in context.Areas.AsNoTracking()
                join project in context.Projects.AsNoTracking() on area.ProjectId equals project.Id into projectOrNone
                from project in projectOrNone.DefaultIfEmpty()
                where area.OrganizationId == organizationId
                orderby EF.Functions.Collate(area.Name, OrganizationsCollation.Russian), area.Id
                select new AreaTreeRow(
                    area.Id,
                    area.ParentId,
                    area.Name,
                    area.Description,
                    area.Type,
                    area.ProjectId,
                    project == null ? null : project.Status == ProjectStatus.Active))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AreaTeamRow>> ListTeamsAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken) =>
        await (
                from link in context.AreaTeams.AsNoTracking()
                join team in context.ActiveTeamsOf(organizationId) on link.TeamId equals team.Id
                where link.OrganizationId == organizationId && link.AreaId == areaId
                orderby EF.Functions.Collate(team.Name, OrganizationsCollation.Russian), team.Id
                select new AreaTeamRow(team.Id, team.Name))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListAreaIdsOfTeamAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) =>
        await context.AreaTeams
            .AsNoTracking()
            .Where(link => link.OrganizationId == organizationId && link.TeamId == teamId)
            .Select(link => link.AreaId)
            .ToListAsync(cancellationToken);
}
