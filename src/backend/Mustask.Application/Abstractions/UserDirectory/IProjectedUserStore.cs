using Mustask.Domain.UserDirectory;

namespace Mustask.Application.Abstractions.UserDirectory;

/// <summary>
/// The write side of the user projection, for the sign-in use case only (<c>Features/UserDirectory</c>) — consumers
/// read through <see cref="IUserDirectory"/> and never touch this (BR-UP-1; an architecture rule holds it). Every
/// method runs inside the caller's transaction (<see cref="IUnitOfWork.ExecuteInTransactionAsync{TResult}"/>).
/// </summary>
/// <remarks>
/// Lock discipline (ADR-0025 § 6, kept by ADR-0034): a writer takes the per-user lock of the id before it reads the
/// user for update — also when the user is not held yet, which a row lock cannot cover — so two sign-ins of one person
/// apply one after the other, and the second finds the first one's row instead of adding a duplicate.
/// </remarks>
public interface IProjectedUserStore
{
    /// <summary>Takes the per-user lock of <paramref name="id"/> and loads that user for update; null when not held.</summary>
    Task<ProjectedUser?> LockAndLoadAsync(string id, CancellationToken cancellationToken);

    /// <summary>Adds a user the first sign-in created; saved with the transaction.</summary>
    void Add(ProjectedUser user);
}
