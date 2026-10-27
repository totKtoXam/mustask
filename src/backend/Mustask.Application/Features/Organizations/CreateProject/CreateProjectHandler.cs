using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.CreateProject;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects</c> (US-3 "A project is created ready to use", "The key is required and
/// well-formed", "The key is unique in the organization", "The name is unique in the organization", "A deactivated project's name
/// stays reserved"; feat-028 US-2 "a project with a name and a key only", "description and teams in one operation").
/// <b>Creating a project creates no team and no team membership — the creator's included</b> (BR-OL-2). The request may carry a description, the teams and (mod-018) the areas to link, linked in the same operation.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.1, § 0.5): the second codes <b>first of all</b> — <c>team.link</c> and
/// <c>project.link</c> when <c>teamIds</c> is not empty (D-7; the route declared <c>organizations.project.write</c>); the
/// organization of the address; no rule about the addressed object alone (a create has none); the values of the body, <c>name</c>,
/// <c>key</c> and <c>description</c> with every reason at once; the teams named (404 <c>team.not_found</c>: an active team of the
/// organization each, all or nothing); the uniqueness pre-checks, the name before the key (api-contract § 2.3: "when both the name
/// and the key are taken, the name's code is answered"); the save.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 7.2): the teams named <c>FOR SHARE</c> in one statement ordered by id (the binder's mode, step 2),
/// re-checked as active, so a deletion of one of them either commits first (the whole creation is refused 404, no project) or waits for
/// this creation and removes the link it wrote with the rest. The write otherwise only reads the status of its container, the
/// organization (ADR-0026 rule 8), and inserts. The unique indexes close the races (ADR-0011 echelon 1): the name and key pre-checks
/// are two round-trips, and a concurrent creation in between fails the insert on its index, which the repository answers with the
/// pre-check's code (plan-backend § 6.3).
/// </para>
/// <para>
/// BR-M1-25 ("the creator joins the project's team") lapsed with the project's team: nothing depends on the creator's membership any
/// more, so the rollback to the organization's not-found that guarded it is gone.
/// </para>
/// </remarks>
public sealed class CreateProjectHandler(
    IOrganizationReadModel organizations,
    ITeamRepository teams,
    IProjectTeamRepository links,
    IProjectRepository projects,
    IAreaRepository areas,
    IAreaLinkRepository areaLinks,
    IOrganizationAccess access,
    IClock clock,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<CreateProjectResult> Handle(CreateProjectCommand command, CancellationToken cancellationToken)
    {
        // 0. The second codes, first of all (D-7): a project that links needs both ends' link codes.
        if (command.TeamIds is { Count: > 0 }
            && (!access.Holds(PermissionCatalog.OrganizationsTeamLink) || !access.Holds(PermissionCatalog.OrganizationsProjectLink)))
        {
            throw new ForbiddenException(OrganizationErrorCodes.AccessForbidden);
        }

        if (command.AreaIds is { Count: > 0 }
            && (!access.Holds(PermissionCatalog.OrganizationsAreaLink) || !access.Holds(PermissionCatalog.OrganizationsProjectLink)))
        {
            throw new ForbiddenException(OrganizationErrorCodes.AccessForbidden);
        }

        return await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address. The scoped check has answered for it already; the handler checks again,
                // because the filter is the boundary and not the only check (plan-backend § 7.3), and a project must never
                // be written into an organization that is not active (ADR-0026 rule 5, BR-M1-20).
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId)
                    || await organizations.FindActiveAsync(organizationId, ct) is null)
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                // 2. Rules about the addressed object alone: none, a create addresses only its container.

                // 3. Values in the body: one ValidationException with every field reason at once. The key is judged exactly
                // as sent: not trimmed and not upper-cased (BR-M1-1). The description is kept as typed (BR-OL-3).
                var errors = new Dictionary<string, IReadOnlyList<FieldError>>();

                if (!EntityName.TryCreate(command.Name, out var name))
                {
                    errors["name"] = [EntityName.LengthReason];
                }

                if (ProjectKey.Validate(command.Key) is { } keyReason)
                {
                    errors["key"] = [new FieldError(keyReason)];
                }

                if (!EntityDescription.TryCreate(command.Description, out var description))
                {
                    errors["description"] = [EntityDescription.LengthReason];
                }

                if (errors.Count > 0)
                {
                    throw new ValidationException(OrganizationErrorCodes.ValidationFailed, errors);
                }

                // 4. References in the body: the teams to link — each an active team of this organization (404), all or nothing.
                var teamIds = new HashSet<Guid>();

                foreach (var raw in command.TeamIds ?? [])
                {
                    if (!AddressedId.TryParse(raw, out var teamId))
                    {
                        throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                    }

                    teamIds.Add(teamId);
                }

                // The areas named (404), locked FOR SHARE before the teams (ADR-0028 rule 8), each judged by the lock statement's answer.
                var areaIds = AreaRequests.ParseIds(command.AreaIds, OrganizationErrorCodes.AreaNotFound);

                if (areaIds.Count > 0 && (await areas.LockManyForShareAsync(organizationId, areaIds, ct)).Count != areaIds.Count)
                {
                    throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
                }

                if (teamIds.Count > 0)
                {
                    var locked = await teams.LockManyForShareAsync(organizationId, teamIds, ct);

                    if (locked.Count != teamIds.Count || locked.Any(team => !team.IsActive))
                    {
                        throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                    }
                }

                // 5. The uniqueness pre-checks (BR-M1-1): the name among projects of any status, ignoring case and
                // surrounding spaces, then the key among projects of any status. The name answers first.
                var nameHolder = await projects.FindNameHolderAsync(organizationId, name.Normalized, exceptId: null, ct);

                if (nameHolder is { } holderStatus)
                {
                    throw new ConflictException(
                        holderStatus == ProjectStatus.Deactivated
                            ? OrganizationErrorCodes.ProjectNameTakenByDeactivated
                            : OrganizationErrorCodes.ProjectNameTaken);
                }

                // Validate has refused a null key, so the key is present and well formed here.
                var key = command.Key!;

                if (await projects.IsKeyTakenAsync(organizationId, key, ct))
                {
                    throw new ConflictException(
                        OrganizationErrorCodes.ProjectKeyTaken,
                        new ErrorParams { ["key"] = ErrorParam.Text(key) });
                }

                // The project; no area and no team and no team membership
                // (BR-OL-2); the settings name no default team — two teams chosen name none, one is the default by itself
                // (BR-OL-7). The creator is the token's subject (BR-M1-21).
                var now = clock.UtcNow;
                var creatorId = currentUser.SubjectId;
                var created = Project.Create(organizationId, name, key, now, creatorId, description);
                projects.Add(created);

                foreach (var teamId in teamIds)
                {
                    links.Add(ProjectTeam.Link(organizationId, created.Id, teamId, creatorId, now));
                }

                foreach (var areaId in areaIds)
                {
                    areaLinks.Add(AreaProject.Link(organizationId, areaId, created.Id, creatorId, now));
                }

                // 6. Save. A concurrent creation of the same name or key that committed first fails this insert on its unique
                // index, and the repository answers with the pre-check's code.
                await projects.SaveAsync(ct);

                var project = created;

                return new CreateProjectResult(
                    new ProjectResponse(project.Id, project.OrganizationId, project.Name, project.Key, project.Description));
            },
            cancellationToken);
    }
}
