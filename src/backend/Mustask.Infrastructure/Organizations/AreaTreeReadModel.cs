using Mustask.Application.Abstractions.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// Two queries whatever the tree's size (NFR Volumes): the project's
/// reachability, then every node, siblings in display order. The pre-order and the
/// paths are assembled from these rows by the handler (plan-backend § 6.4). No team is joined: an area owns
/// nothing (mod-017 BR-OT-10).
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
            .Select(area => new AreaTreeRow(area.Id, area.ParentId, area.Name))
            .ToListAsync(cancellationToken);
    }
}
