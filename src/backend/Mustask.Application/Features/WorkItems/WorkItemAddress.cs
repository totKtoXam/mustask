using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.WorkItems;

/// <summary>An addressed work item as the outside-in resolution leaves it: the organization, the project and the identifier.</summary>
public sealed record AddressedWorkItem(Guid OrganizationId, Guid ProjectId, string ProjectKey, WorkItemIdentifier Identifier);

/// <summary>
/// The address of one work item, resolved from the outside in (mod-003 api-contract § 0.3), shared by the mod-005 slices that
/// act on an item by its identifier (Delete, Restore, Purge) so that the three answer one not-found in one order — the
/// organization, then the project (malformed, unknown, of another organization or not reachable), then the identifier
/// (malformed, or a project key that is not the addressed project's), then, once the item is read, its type key. Every miss
/// is the level's own not-found, so a purged item, another project's identifier and an unknown number answer alike.
/// Lives in the module's root, not in a slice: a slice takes shared code from Abstractions, the domain or here.
/// </summary>
public static class WorkItemAddress
{
    public static async Task<AddressedWorkItem> ResolveAsync(
        IOrganizationStructure structure, string? organizationId, string? projectId, string? identifier, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(structure);

        if (!AddressedId.TryParse(organizationId, out var organization))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(projectId, out var project)
            || await structure.FindProjectAsync(project, cancellationToken) is not { IsReachable: true } found
            || found.OrganizationId != organization)
        {
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }

        if (!WorkItemIdentifier.TryParse(identifier, out var parsed)
            || !string.Equals(parsed.ProjectKey, found.Key, StringComparison.Ordinal))
        {
            throw new NotFoundException(WorkItemErrorCodes.WorkItemNotFound);
        }

        return new AddressedWorkItem(organization, project, found.Key, parsed);
    }

    /// <summary>The item's type key must be the identifier's: another key names no item (mod-003 D-5).</summary>
    /// <exception cref="NotFoundException"><see cref="WorkItemErrorCodes.WorkItemNotFound"/>.</exception>
    public static void EnsureTypeKey(WorkItemTypeStates type, WorkItemIdentifier identifier)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (!string.Equals(type.TypeKey, identifier.TypeKey, StringComparison.Ordinal))
        {
            throw new NotFoundException(WorkItemErrorCodes.WorkItemNotFound);
        }
    }
}
