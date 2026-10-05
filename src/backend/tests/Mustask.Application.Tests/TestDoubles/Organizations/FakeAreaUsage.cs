using Mustask.Application.Abstractions.Organizations;

namespace Mustask.Application.Tests.TestDoubles.Organizations;

/// <summary>The areas some other module files something under (mod-003: a work item), as a test lists them.</summary>
public sealed class FakeAreaUsage : IAreaUsage
{
    public HashSet<(Guid ProjectId, Guid AreaId)> InUse { get; } = [];

    public Task<bool> IsInUseAsync(Guid projectId, Guid areaId, CancellationToken cancellationToken) =>
        Task.FromResult(InUse.Contains((projectId, areaId)));
}
