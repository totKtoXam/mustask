using Mustask.Domain.Process;

namespace Mustask.Application.Abstractions.Process;

/// <summary>
/// Storage half of the seed (slice <c>SeedProcess</c>): the one place that knows
/// how a process becomes rows, and how "insert once per organization" is
/// guaranteed under concurrency. The implementation joins the caller's ambient
/// unit of work and never begins, commits or rolls back a transaction itself.
/// </summary>
public interface IProcessSeedStore
{
    /// <summary>
    /// Persists <paramref name="candidate"/> unless its organization already has a
    /// process, and returns the identity of the organization's process: the
    /// candidate's when it was stored, the existing one otherwise (nothing is
    /// created then). When the call throws, nothing of the candidate remains.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// There is no ambient transaction, or the context has pending changes: the
    /// call writes nothing in either case.
    /// </exception>
    Task<ProcessId> InsertIfAbsentAsync(ProcessDefinition candidate, CancellationToken cancellationToken);
}
