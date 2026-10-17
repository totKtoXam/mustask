using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>The write side of projects. Locks before it loads, as <see cref="IOrganizationRepository"/> does.</summary>
public interface IProjectRepository
{
    /// <summary>
    /// The status of the project of the organization whose normalized name is
    /// <paramref name="normalizedName"/>, other than <paramref name="exceptId"/>;
    /// <c>null</c> when the name is free. Tracks nothing.
    /// </summary>
    Task<ProjectStatus?> FindNameHolderAsync(Guid organizationId, string normalizedName, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>Whether a project of any status in the organization holds <paramref name="key"/> (BR-M1-1).</summary>
    Task<bool> IsKeyTakenAsync(Guid organizationId, string key, CancellationToken cancellationToken);

    /// <summary>
    /// Whether the project is active, in that organization, and the organization is
    /// active (ADR-0026 rule 3). No lock: a write beneath only reads its
    /// containers' status (ADR-0026 rule 8).
    /// </summary>
    Task<bool> IsReachableAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken);

    /// <summary>The project of any status in that organization, locked <c>FOR NO KEY UPDATE</c>; <c>null</c> when there is none.</summary>
    Task<Project?> LockAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// mod-017 (BR-OT-12): clears the project's default-team setting if it names <paramref name="teamId"/>, with one statement;
    /// the team deletion's step, after it holds every team of the project. Writes the project row (<c>NO KEY UPDATE</c>), never
    /// before a team row (plan-backend § 7.1).
    /// </summary>
    Task ClearDefaultTeamAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken);

    /// <summary>Adds the project with its team and its root.</summary>
    void Add(NewProject project);

    /// <inheritdoc cref="IOrganizationRepository.SaveAsync"/>
    Task SaveAsync(CancellationToken cancellationToken);
}
