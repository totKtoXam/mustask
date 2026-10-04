using Mustask.Application.Abstractions.Process;
using Mustask.Domain.Process;

namespace Mustask.Application.Features.Process.SeedProcess;

/// <summary>
/// US-1: seeds an organization's complete process (<see cref="IProcessSeed"/>). It
/// never touches a transaction: it builds the candidate in the domain, where the
/// identifier is checked, and hands it to the store, which joins the caller's
/// unit of work.
/// </summary>
public sealed class SeedProcessHandler(IProcessSeedStore store) : IProcessSeed
{
    public async Task<ProcessId> SeedAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        // A malformed identifier throws here, before the store is called.
        var candidate = ProcessDefinition.Seed(ProcessId.New(), organizationId);

        return await store.InsertIfAbsentAsync(candidate, cancellationToken);
    }
}
