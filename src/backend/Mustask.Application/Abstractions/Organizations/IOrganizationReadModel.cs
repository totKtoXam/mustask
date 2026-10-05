using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// Reads of organizations, each with its intent named in one place (ADR-0026
/// rule 4): a list of one status, an active one by id, and the status alone
/// for the organization-context filter. Lists are in display order: by name in
/// Russian collation, then by id (api-contract § 0.6).
/// </summary>
public interface IOrganizationReadModel
{
    Task<IReadOnlyList<OrganizationRow>> ListAsync(OrganizationStatus status, CancellationToken cancellationToken);

    /// <summary>The organization when it is active; otherwise <c>null</c>.</summary>
    Task<OrganizationRow?> FindActiveAsync(Guid organizationId, CancellationToken cancellationToken);

    /// <summary>The organization's status, or <c>null</c> when there is none. The organization-context filter's one read.</summary>
    Task<OrganizationStatus?> FindStatusAsync(Guid organizationId, CancellationToken cancellationToken);
}

/// <summary>One organization as the panel sees it.</summary>
public sealed record OrganizationRow(Guid Id, string Name);
