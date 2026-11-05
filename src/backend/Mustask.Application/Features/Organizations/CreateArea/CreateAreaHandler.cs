using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.CreateArea;

/// <summary>
/// The command of <c>POST /api/organizations/{organizationId}/areas</c>. A <c>null</c> parent is the top level, where only a
/// department stands (a project's node is created with its project); a parent is a project's node or a sub-area.
/// </summary>
public sealed record CreateAreaCommand(
    string? OrganizationId,
    string? Name,
    string? ParentId = null,
    string? Description = null,
    IReadOnlyList<string?>? TeamIds = null);

/// <summary>The node as every area write returns it (mod-018 api-contract § 1, <c>Area</c>).</summary>
public sealed record AreaResponse(Guid Id, Guid? ParentId, string Type, Guid? ProjectId, string Name, string? Description, string Path);

/// <summary>The handler's outcome: the body of the answer.</summary>
public sealed record CreateAreaResult(AreaResponse Response);

/// <summary>
/// <c>POST /api/organizations/{organizationId}/areas</c> (mod-018 api-contract § 2, US-1): a new department (no parent) or a new
/// sub-area (a parent in a project), optionally linked to teams in the same transaction (a department only: OQ-8 A, all or nothing).
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (BR-AW-15): the second codes first of all (<c>area.create</c> for a department; <c>area.link</c> with
/// <c>team.link</c> for teams named); the organization; the parent (404); the values (400, every reason at once); a deactivated
/// project's parent (404 <c>project.not_found</c>); the parent's type (409 <c>type_not_allowed</c>) and teams named under a sub-area
/// (409 <c>link_type_not_allowed</c>); the teams (404); then 409 — the depth, then the sibling name.
/// </para>
/// <para>
/// <b>Locks</b> (ADR-0031, ADR-0028 rule 8): the organization's tree lock, then the parent <c>FOR SHARE</c> (a creation under a parent
/// being moved or deleted waits and then reads what it left), then the teams <c>FOR SHARE</c>, and the tree is read after them. A
/// parent judged present by the unlocked read is judged again by the lock statement's own answer. Two creations of one name
/// serialize on the tree lock; the sibling index is the backstop.
/// </para>
/// </remarks>
public sealed class CreateAreaHandler(
    IOrganizationReadModel organizations,
    IProjectRepository projects,
    ITeamRepository teams,
    IAreaRepository areas,
    IAreaLinkRepository areaLinks,
    IOrganizationAccess access,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<CreateAreaResult> Handle(CreateAreaCommand command, CancellationToken cancellationToken)
    {
        // 0. The second codes, first of all (D-7): a role lacking one learns nothing about any id, and nothing changes.
        var teamsNamed = command.TeamIds is { Count: > 0 };

        if ((command.ParentId is null && !access.Holds(PermissionCatalog.OrganizationsAreaCreate))
            || (teamsNamed
                && (!access.Holds(PermissionCatalog.OrganizationsAreaLink) || !access.Holds(PermissionCatalog.OrganizationsTeamLink))))
        {
            throw new ForbiddenException(OrganizationErrorCodes.AccessForbidden);
        }

        return await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address: the organization, then the parent, unlocked (the lock comes after the values).
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId)
                    || await organizations.FindActiveAsync(organizationId, ct) is null)
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                Guid? parentId = null;

                if (command.ParentId is not null)
                {
                    if (!AddressedId.TryParse(command.ParentId, out var parsedParentId)
                        || await areas.FindAsync(organizationId, parsedParentId, ct) is null)
                    {
                        throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
                    }

                    parentId = parsedParentId;
                }

                // 2. Rules about the addressed object alone: none.

                // 3. Values in the body: one ValidationException with every field reason.
                var errors = new Dictionary<string, IReadOnlyList<FieldError>>();

                if (!EntityName.TryCreate(command.Name, out var name))
                {
                    errors["name"] = [EntityName.LengthReason];
                }

                if (!EntityDescription.TryCreate(command.Description, out var description))
                {
                    errors["description"] = [EntityDescription.LengthReason];
                }

                if (errors.Count > 0)
                {
                    throw new ValidationException(OrganizationErrorCodes.ValidationFailed, errors);
                }

                var teamIds = AreaRequests.ParseIds(command.TeamIds, OrganizationErrorCodes.TeamNotFound);

                // The locks, in rule 8's order: the tree, the parent, then the teams.
                await areas.LockTreeAsync(organizationId, ct);

                Area? parent = null;

                if (parentId is { } lockedParentId)
                {
                    parent = await areas.LockForShareAsync(organizationId, lockedParentId, ct)
                        ?? throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);

                    // A sub-area of a deactivated project is not created (BR-AW-10): the project is not found.
                    await AreaRequests.EnsureProjectActiveAsync(projects, organizationId, parent, ct);
                }

                // 4. The type rules (409): a sub-area stands only below a project's node or a sub-area (OQ-7 A); teams link to a
                // department only (OQ-8 A), and a department is the only node created without a parent.
                if (parent is not null && parent.Type == AreaType.Department)
                {
                    throw new ConflictException(OrganizationErrorCodes.AreaTypeNotAllowed);
                }

                if (parent is not null && teamIds.Count > 0)
                {
                    throw new ConflictException(OrganizationErrorCodes.AreaLinkTypeNotAllowed);
                }

                if (teamIds.Count > 0)
                {
                    var locked = await teams.LockManyForShareAsync(organizationId, teamIds, ct);

                    if (locked.Count != teamIds.Count || locked.Any(team => !team.IsActive))
                    {
                        throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                    }
                }

                // 5. The tree as the locks hold it, then the depth and the sibling name (409).
                var tree = new AreaTree(await areas.GetNodesAsync(organizationId, ct));
                tree.EnsureCanHold(parentId, subtreeHeight: 0);

                if (await areas.IsSiblingNameTakenAsync(organizationId, parentId, name.Normalized, exceptId: null, ct))
                {
                    throw new ConflictException(OrganizationErrorCodes.AreaNameTaken);
                }

                var now = clock.UtcNow;
                var creator = currentUser.SubjectId;
                var area = parent is null
                    ? Area.CreateDepartment(organizationId, name, description, creator, now)
                    : Area.CreateSubArea(parent, name, description, creator, now);
                areas.Add(area);

                foreach (var teamId in teamIds)
                {
                    areaLinks.Add(AreaTeam.Link(organizationId, area.Id, teamId, creator, now));
                }

                // 6. One flush. A concurrent creation of the same name that committed first fails the sibling index and answers
                // with the pre-check's code (OrganizationsSaveChanges).
                await areas.SaveAsync(ct);

                var path = parentId is { } above ? $"{tree.PathOf(above)}{AreaTree.PathSeparator}{area.Name}" : area.Name;

                return new CreateAreaResult(
                    new AreaResponse(area.Id, area.ParentId, AreaRequests.TypeName(area.Type), area.ProjectId, area.Name, area.Description, path));
            },
            cancellationToken);
    }
}
