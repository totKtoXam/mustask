using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>Reads of projects through the reachability specification (ADR-0026 rules 3 and 4).</summary>
public interface IProjectReadModel
{
    /// <summary>The projects of one status of an active organization, in display order; empty when the organization is not active.</summary>
    Task<IReadOnlyList<ProjectRow>> ListAsync(Guid organizationId, ProjectStatus status, CancellationToken cancellationToken);

    /// <summary>The project when it is reachable in that organization; otherwise <c>null</c>.</summary>
    Task<ProjectRow?> FindReachableAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken);
}

/// <summary>One project as the panel sees it.</summary>
public sealed record ProjectRow(Guid Id, Guid OrganizationId, string Name, string Key);
