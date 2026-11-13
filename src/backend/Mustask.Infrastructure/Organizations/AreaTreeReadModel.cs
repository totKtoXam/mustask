using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// The typed area tree and the teams standing in a department, one statement per read whatever the number of nodes (mod-018 NFR Query
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
                    project == null ? null : project.Status == ProjectStatus.Active,
                    project == null ? null : project.Key,
                    area.InheritsAccess,
                    area.Type == AreaType.Project ? (project == null ? null : project.Color) : area.Color))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AreaTeamRow>> ListTeamsAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken) =>
        await context.ActiveTeamsOf(organizationId)
            .AsNoTracking()
            .Where(team => team.DepartmentId == areaId)
            .OrderBy(team => EF.Functions.Collate(team.Name, OrganizationsCollation.Russian))
            .ThenBy(team => team.Id)
            .Select(team => new AreaTeamRow(team.Id, team.Name, team.Color))
            .ToListAsync(cancellationToken);
}
