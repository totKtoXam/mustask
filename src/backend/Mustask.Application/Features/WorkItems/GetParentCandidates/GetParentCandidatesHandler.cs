using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.WorkItems.GetParentCandidates;

/// <summary>
/// The «Родитель» picker's search (feat-001 BR-H-6, BR-H-14; api-contract
/// § 3.4): the items of the addressed item's project that may be its parent —
/// of a type its level allows (BR-H-2), or only of the level directly above it
/// for the backlog's dialog — never the item itself, never an item in «Удалена»
/// (BR-H-8). Its descendants are offered: the write refuses a cycle with its
/// path (US-2).
/// </summary>
/// <remarks>
/// Not found from the outside in, as the item's read: the organization (the
/// filter has answered), the project, the item. Reads only, no transaction.
/// </remarks>
public sealed class GetParentCandidatesHandler(
    IOrganizationStructure structure,
    IProcessDefinitionReadModel processes,
    IWorkItemReadModel readModel)
{
    public async Task<ParentCandidatesResponse> Handle(GetParentCandidatesQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(query.ProjectId, out var projectId)
            || await structure.FindProjectAsync(projectId, cancellationToken) is not { IsReachable: true } project
            || project.OrganizationId != organizationId)
        {
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }

        if (!WorkItemIdentifier.TryParse(query.Identifier, out var identifier)
            || !string.Equals(identifier.ProjectKey, project.Key, StringComparison.Ordinal)
            || await readModel.FindHeadAsync(projectId, identifier.Number, cancellationToken) is not { } item)
        {
            throw new NotFoundException(WorkItemErrorCodes.WorkItemNotFound);
        }

        var process = await processes.FindByOrganizationAsync(organizationId, cancellationToken)
            ?? throw new InvalidOperationException($"The organization {organizationId} has no process.");
        var hierarchy = ProcessHierarchy.Of(process);

        if (!string.Equals(hierarchy.Of(item.TypeId).Key, identifier.TypeKey, StringComparison.Ordinal))
        {
            throw new NotFoundException(WorkItemErrorCodes.WorkItemNotFound);
        }

        var types = query.LevelAbove ? hierarchy.LevelAboveTypeIds(item.TypeId) : hierarchy.ParentTypeIds(item.TypeId);
        var text = (query.Text ?? string.Empty).Trim();

        // No title is longer than its limit and no identifier comes near it, so a longer text matches nothing.
        if (types.Count == 0 || text.Length > WorkItemLimits.TitleMaxLength)
        {
            return new ParentCandidatesResponse([], MoreMatched: false);
        }

        var found = await readModel.FindParentCandidatesAsync(
            new ParentCandidateSearch(projectId, project.Key, item.Id, types, text.ToLowerInvariant(), WorkItemLimits.ParentCandidatesShown),
            cancellationToken);

        return new ParentCandidatesResponse(found.Items, found.MoreMatched);
    }
}
