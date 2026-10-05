using Mustask.Application.Abstractions.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// Two queries whatever the tree's size (NFR Volumes): the project's
/// reachability, then every node with the names of the teams it refers to,
/// siblings in display order. The pre-order, the paths and the resolved owners
/// are assembled from these rows by the handler (plan-backend § 6.4).
/// </summary>
internal sealed class AreaTreeReadModel(MustaskDbContext context) : IAreaTreeReadModel
{
    public async Task<IReadOnlyList<AreaTreeRow>?> GetNodesAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        if (!await context.ReachableProjects(organizationId).AnyAsync(project => project.Id == projectId, cancellationToken))
        {
            return null;
        }

        return await context.Areas
            .AsNoTracking()
            .Where(area => area.ProjectId == projectId)
            .OrderBy(area => EF.Functions.Collate(area.Name, OrganizationsCollation.Russian))
            .ThenBy(area => area.Id)
            .Select(area => new AreaTreeRow(
                area.Id,
                area.ParentId,
                area.Name,
                area.OwnerTeamId,
                context.Teams.Where(team => team.Id == area.OwnerTeamId).Select(team => team.Name).FirstOrDefault(),
                area.DefaultOfTeamId,
                context.Teams.Where(team => team.Id == area.DefaultOfTeamId).Select(team => team.Name).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }
}
