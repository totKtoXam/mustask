using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// Reads of organizations, each with its intent named in one place (ADR-0026
/// rule 4): a list of one status narrowed to the caller's memberships, and an
/// active one by id. Lists are in display order: by name in Russian collation,
/// then by id (api-contract § 0.6). The scoped check's own read is
/// <see cref="IOrganizationAccessReadModel"/>.
/// </summary>
public interface IOrganizationReadModel
{
    /// <summary>
    /// The organizations of <paramref name="status"/> the user is a member of (mod-016 BR-M1-7, BR-M1-35). With
    /// <paramref name="onlyRoles"/> the list keeps only those where the user's role is one of them — the deactivated list asks
    /// for the roles that hold <c>organizations.organization.deactivate</c>; <c>null</c> means any role.
    /// </summary>
    Task<IReadOnlyList<OrganizationRow>> ListAsync(
        OrganizationStatus status,
        string userId,
        IReadOnlyCollection<OrganizationRole>? onlyRoles,
        CancellationToken cancellationToken);

    /// <summary>The organization when it is active; otherwise <c>null</c>.</summary>
    Task<OrganizationRow?> FindActiveAsync(Guid organizationId, CancellationToken cancellationToken);
}

/// <summary>One organization as the panel sees it.</summary>
public sealed record OrganizationRow(Guid Id, string Name);
