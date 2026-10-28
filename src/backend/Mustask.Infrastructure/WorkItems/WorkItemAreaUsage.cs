using Mustask.Application.Abstractions.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.WorkItems;

/// <summary>
/// M1's <see cref="IAreaUsage"/>, answered by M3 (mod-003 api-contract § 7):
/// any work item of the project in the area, whatever its state (BR-M3-24) — a deleted item still holds its area, until
/// mod-018's decision (mod-005 spec § Surfaces and writes). Served by <see cref="WorkItemsConstraints.ProjectArea"/>.
/// </summary>
internal sealed class WorkItemAreaUsage(MustaskDbContext context) : IAreaUsage
{
    public Task<bool> IsInUseAsync(Guid projectId, Guid areaId, CancellationToken cancellationToken) =>
        WorkItemSets.InAnyState(context).AnyAsync(item => item.ProjectId == projectId && item.AreaId == areaId, cancellationToken);
}
