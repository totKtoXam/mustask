using Mustask.Domain.Process;

namespace Mustask.Application.Features.Process;

/// <summary>
/// The seam through which the organization-creation flow of <c>mod-002</c> gets
/// an organization's complete, ready-to-use process (US-1, BR-M2-1). It is an
/// explicit interface at the module root, not inside a slice, so a slice of
/// another module can depend on it without referencing a slice of this one
/// (docs/conventions/backend.md: modules talk through explicit interfaces).
/// It is not HTTP: no request seeds a process (BR-M2-13). The contract, with
/// the obligations on both sides, is
/// <c>docs/features/mod-001-process-seed/api-contract.md</c> § 2.
/// </summary>
/// <remarks>
/// <para>
/// <b>Registration:</b> scoped. Resolve it from the same DI scope as the
/// <c>IUnitOfWork</c> it runs under: both must share one database context.
/// </para>
/// <para>
/// <b>The caller's obligations.</b> (1) Call it inside
/// <c>IUnitOfWork.ExecuteInTransactionAsync</c>: without an ambient transaction
/// it throws <see cref="InvalidOperationException"/> and writes nothing.
/// (2) Call it before tracking any change of the caller's own (seed first, then
/// create the organization with the returned identity): with pending changes it
/// throws <see cref="InvalidOperationException"/> and writes nothing. (3) Treat
/// an exception as a failed seed: let it leave the unit of work, so the
/// organization's creation fails as a whole and never becomes visible without
/// its process (BR-M2-8). (4) Seed only while creating an organization (BR-M2-13).
/// </para>
/// </remarks>
public interface IProcessSeed
{
    /// <summary>
    /// Seeds the complete process of the organization, or returns the identity of
    /// the one it already has (BR-M2-9). The seed joins the caller's transaction and
    /// never begins, commits or rolls it back (status.md D-10): it works inside a
    /// savepoint, flushes its own rows before it returns, and its rows commit or
    /// roll back with the caller's operation.
    /// </summary>
    /// <param name="organizationId">An opaque key (doc-007 R-5). Any identifier other than <see cref="Guid.Empty"/> is well formed.</param>
    /// <returns>The <see cref="ProcessId"/> of the organization's process.</returns>
    /// <exception cref="ArgumentException"><paramref name="organizationId"/> is <see cref="Guid.Empty"/>; nothing is created.</exception>
    /// <exception cref="InvalidOperationException">The caller broke an obligation above; nothing is created.</exception>
    /// <remarks>
    /// On any exception nothing of a process remains for the identifier, and the
    /// caller receives no identity (BR-M2-8). Two racing calls leave one process:
    /// the later one waits on the database until the earlier transaction ends, then
    /// returns the winner's identity if it committed, and creates the process itself
    /// if the winner rolled back.
    /// </remarks>
    Task<ProcessId> SeedAsync(Guid organizationId, CancellationToken cancellationToken);
}
