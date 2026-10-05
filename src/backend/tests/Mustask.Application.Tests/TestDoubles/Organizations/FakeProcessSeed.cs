using Mustask.Application.Features.Process;
using Mustask.Domain.Process;

namespace Mustask.Application.Tests.TestDoubles.Organizations;

/// <summary>
/// mod-001's seed seam as <c>CreateOrganization</c> sees it: records the ids it
/// was called with, returns a fresh process identity, or throws
/// <see cref="Failure"/> to stand in for a failed seed (US-1).
/// </summary>
public sealed class FakeProcessSeed(InMemoryOrganizations store) : IProcessSeed
{
    public List<Guid> SeededOrganizations { get; } = [];

    /// <summary>When set, the seed throws it instead of seeding.</summary>
    public Exception? Failure { get; set; }

    public Task<ProcessId> SeedAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        store.Record($"seed {organizationId}");

        if (Failure is { } failure)
        {
            throw failure;
        }

        SeededOrganizations.Add(organizationId);

        return Task.FromResult(ProcessId.New());
    }
}
