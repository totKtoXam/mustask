using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.DeleteArea;

/// <summary>The command of <c>DELETE /api/organizations/{organizationId}/areas/{areaId}</c>.</summary>
public sealed record DeleteAreaCommand(string? OrganizationId, string? AreaId);

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/areas/{areaId}</c> (mod-018 api-contract § 2, US-2, BR-AW-9): a department with no
/// project node and no team (BR-TD-5) or a sub-area with no child and no work item (in any state, mod-003 BR-M3-24) is deleted outright 
/// (it never has a team link: teams stand in a department by their own reference). A project's node is deleted only with its project (OQ-5 A): <c>root_is_fixed</c>.
/// </summary>
/// <remarks>
/// <b>The order of answers</b>: the organization; the node (404); a deactivated project (404 <c>project.not_found</c>); a project's
/// node (409 <c>root_is_fixed</c>); a child or an item (409 <c>not_empty</c>). <b>Locks</b>: the tree lock, then the node
/// <c>FOR UPDATE</c> (judged present by the lock statement's own answer), and the children and the items are looked for after it — a
/// creation under the node holds the tree lock and the node <c>FOR SHARE</c>, and a save that puts an item in a sub-area locks it
/// <c>FOR SHARE</c>, so each either committed before this lock or waits for this deletion and finds the node gone.
/// </remarks>
public sealed class DeleteAreaHandler(
    IOrganizationReadModel organizations,
    IProjectRepository projects,
    IAreaRepository areas,
    IAreaUsage areaUsage,
    ITeamRepository teams,
    IUnitOfWork unitOfWork)
{
    public async Task Handle(DeleteAreaCommand command, CancellationToken cancellationToken) =>
        _ = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId)
                    || await organizations.FindActiveAsync(organizationId, ct) is null)
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                if (!AddressedId.TryParse(command.AreaId, out var areaId)
                    || await areas.FindAsync(organizationId, areaId, ct) is null)
                {
                    throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
                }

                await areas.LockTreeAsync(organizationId, ct);

                var area = await areas.LockForUpdateAsync(organizationId, areaId, ct, forDelete: true)
                    ?? throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);

                // 2. The rules about the addressed node alone: a deactivated project's node and sub-areas are read-only (BR-AW-10),
                // a project's node goes only with its project (OQ-5 A). Both before the children, although the node has some.
                await AreaRequests.EnsureProjectActiveAsync(projects, organizationId, area, ct);
                area.EnsureNotProjectNode();

                // 5. The domain rule, read after the lock: a child node or a work item (any state) holds the node.
                var hasChildren = await areas.HasChildrenAsync(organizationId, area.Id, ct);
                var holdsWorkItems = !hasChildren
                    && area is { Type: AreaType.Area, ProjectId: { } projectId }
                    && await areaUsage.IsInUseAsync(projectId, area.Id, ct);

                // BR-TD-5: an active team standing in the department holds it, and its answer comes first. A deleted team keeps no
                // department, so it never counts. Read after the node's lock; a team creation or move holds the tree lock and the
                // department FOR SHARE, so each either committed before this lock or waits for this deletion and finds the node gone.
                var holdsTeams = area.Type == AreaType.Department
                    && await teams.AnyActiveInDepartmentAsync(organizationId, area.Id, ct);

                area.EnsureDeletable(hasChildren, holdsWorkItems, holdsTeams);

                areas.Remove(area);
                await areas.SaveAsync(ct);

                return true;
            },
            cancellationToken);
}
