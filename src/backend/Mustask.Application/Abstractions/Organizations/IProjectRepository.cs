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

    /// <summary>
    /// How many of <paramref name="projectIds"/> are <b>active</b> projects of the organization, in an active organization — one
    /// statement, whatever the number of ids. The caller compares it with the number of distinct ids (a link or a team created with
    /// projects is all or nothing).
    /// </summary>
    Task<int> CountReachableAsync(Guid organizationId, IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);

    /// <summary>The project of any status in that organization, locked <c>FOR NO KEY UPDATE</c>; <c>null</c> when there is none.</summary>
    Task<Project?> LockAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// mod-017 (BR-OT-12): clears the project's default-team setting if it names <paramref name="teamId"/>, with one statement;
    /// the unlink's step, after it holds the link. Writes the project row (<c>NO KEY UPDATE</c>), never before a team or a link row
    /// (plan-backend § 7.1).
    /// </summary>
    Task ClearDefaultTeamAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken);

    /// <summary>
    /// feat-028 BR-OL-9: clears every setting of the organization that names the team, with one statement led by the
    /// organization — the deletion's step, after it holds all of the team's links.
    /// </summary>
    Task ClearDefaultTeamAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken);

    /// <summary>Adds the project. No area and no team comes with it (feat-028 BR-OL-2).</summary>
    void Add(Project project);

    /// <inheritdoc cref="IOrganizationRepository.SaveAsync"/>
    Task SaveAsync(CancellationToken cancellationToken);
}
