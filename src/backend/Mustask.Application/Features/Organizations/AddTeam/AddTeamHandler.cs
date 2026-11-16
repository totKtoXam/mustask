using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.AddTeam;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/teams</c> (feat-028 api-contract § 2.1; US-1 "created with a name only", "created with
/// its optional parts in one operation", "a choice that cannot be made", "names are unique per organization", "two creations of one
/// name"; US-7 "a deleted team's name is free"). A team is created in the organization, in <b>one operation</b> with its optional
/// parts: a description, the people in it and the projects it is linked to. <b>Creating a team creates no project, no area and no
/// membership of the caller's</b> (BR-OL-2): a team created with a name only has no link, no member and owns nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.1, § 0.5), one numbered step each below: the second codes <b>first of all</b> —
/// <c>team.link</c> and <c>project.link</c> when <c>projectIds</c> is not empty, <c>team.write</c> when <c>memberIds</c> is not
/// empty (D-7; the route declared <c>organizations.team.create</c>); the organization; the values (400, every reason at once);
/// the projects named (404 <c>project.not_found</c>, one statement); the members named (409 <c>member_not_in_organization</c>); the
/// name's pre-check (409 <c>name_taken</c>); the save.
/// </para>
/// <para>
/// <b>Locks</b> (ADR-0040 § 1): the tree lock, then the department <c>FOR SHARE</c>; the team itself is new and nothing binds to it yet. Two creations of one name, and a creation
/// against a rename to it, are closed by <c>ix_teams_organization_id_normalized_name</c>, and the loser gets
/// <see cref="OrganizationErrorCodes.TeamNameTaken"/> from the repository's constraint map (plan-backend § 6.3); a membership removed
/// between the member check and the flush fails the composite key onto the organization membership, which answers
/// <c>member_not_in_organization</c>. The team, its links and its memberships are one flush: on any refusal nothing is created and
/// the name stays free. The links record the caller and the time. The projects are only read (ADR-0026 rule 8), so a deactivation
/// racing the creation is "a link just before the deactivation".
/// </para>
/// </remarks>
public sealed class AddTeamHandler(
    IProjectRepository projects,
    ITeamRepository teams,
    IProjectTeamRepository links,
    IAreaRepository areas,
    ITeamMembershipRepository teamMemberships,
    IMemberReadModel members,
    IUserDirectory directory,
    IOrganizationAccess access,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<AddTeamResult> Handle(AddTeamCommand command, CancellationToken cancellationToken)
    {
        // 0. The second codes, first of all (D-7): a role without one learns nothing about any id, and nothing changes. Under today's
        // bundles only «Администратор» holds team.create, so nothing differs.
        if (command.ProjectIds is { Count: > 0 }
            && (!access.Holds(PermissionCatalog.OrganizationsTeamLink) || !access.Holds(PermissionCatalog.OrganizationsProjectLink)))
        {
            throw new ForbiddenException(OrganizationErrorCodes.AccessForbidden);
        }

        if (command.MemberIds is { Count: > 0 } && !access.Holds(PermissionCatalog.OrganizationsTeamWrite))
        {
            throw new ForbiddenException(OrganizationErrorCodes.AccessForbidden);
        }

        return await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address. The scoped check has answered for the organization already; the handler parses it again only
                // to name its container in the reads below.
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                // 2. Values in the body: one ValidationException with every field reason at once.
                var errors = new Dictionary<string, IReadOnlyList<FieldError>>();

                if (!EntityName.TryCreate(command.Name, out var name))
                {
                    errors["name"] = [EntityName.LengthReason];
                }

                if (!EntityDescription.TryCreate(command.Description, out var description))
                {
                    errors["description"] = [EntityDescription.LengthReason];
                }

                // The department is required (BR-TD-1): «Выберите департамент». A malformed id names nothing: the 404 below.
                if (command.DepartmentId is null)
                {
                    errors["departmentId"] = [new FieldError(OrganizationErrorCodes.TeamDepartmentRequired)];
                }

                if (errors.Count > 0)
                {
                    throw new ValidationException(OrganizationErrorCodes.ValidationFailed, errors);
                }

                // 3. The projects named (404): an active project of this organization each — malformed, unknown, foreign and
                // deactivated alike — all or nothing. One statement whatever the number.
                var projectIds = new HashSet<Guid>();

                foreach (var raw in command.ProjectIds ?? [])
                {
                    if (!AddressedId.TryParse(raw, out var projectId))
                    {
                        throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
                    }

                    projectIds.Add(projectId);
                }

                if (projectIds.Count > 0 && await projects.CountReachableAsync(organizationId, projectIds, ct) != projectIds.Count)
                {
                    throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
                }

                // 3a. The department (BR-TD-1), under the tree lock and the department FOR SHARE (ADR-0040 § 1): a creation against the
                // department's deletion ends in one order, because the deletion holds the tree lock first. The department is judged
                // present by the lock statement's own answer: malformed, unknown, foreign and deleted ids alike are 404; a project's node
                // or a sub-area is 409 type_not_allowed.
                if (!AddressedId.TryParse(command.DepartmentId, out var departmentId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
                }

                await areas.LockTreeAsync(organizationId, ct);

                var department = await areas.LockForShareAsync(organizationId, departmentId, ct)
                    ?? throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);

                if (department.Type != AreaType.Department)
                {
                    throw new ConflictException(OrganizationErrorCodes.AreaTypeNotAllowed);
                }

                // 4. The members named (409): each an active member of this organization, known to the directory and active there
                // (the same rule as an add on the team's page). Two statements and the directory's batches, whatever the number.
                var userIds = (command.MemberIds ?? []).Where(id => id is not null).Select(id => id!).Distinct(StringComparer.Ordinal).ToList();

                if ((command.MemberIds ?? []).Any(id => !OrganizationMembership.IsValidUserId(id)))
                {
                    throw new ConflictException(OrganizationErrorCodes.TeamMemberNotInOrganization);
                }

                if (userIds.Count > 0)
                {
                    var lookup = await UserDirectoryBatches.LookupAllAsync(directory, userIds, ct);
                    var organizationMembers = await members.FilterMembersAsync(organizationId, userIds, ct);

                    if (userIds.Any(id => !lookup.Known.TryGetValue(id, out var user) || !user.IsActive || !organizationMembers.Contains(id)))
                    {
                        throw new ConflictException(OrganizationErrorCodes.TeamMemberNotInOrganization);
                    }
                }

                // 5. The uniqueness pre-check (BR-OL-3): the name among the organization's active teams, ignoring letter case and
                // surrounding spaces. A deleted team's name is free (ADR-0026 rule 6), another organization's is no concern.
                if (await teams.IsActiveNameTakenAsync(organizationId, name.Normalized, exceptId: null, ct))
                {
                    throw new ConflictException(OrganizationErrorCodes.TeamNameTaken);
                }

                // BR-TD-20: the colour from the department's active teams, the department's own colour counted as used.
                var inUse = (await teams.ListActiveColorsAsync(organizationId, departmentId, ct)).ToList();
                inUse.Add(department.Color!);

                var created = Team.Create(organizationId, departmentId, ColorPalette.Choose(inUse), name, description);
                teams.Add(created);

                var now = clock.UtcNow;

                foreach (var projectId in projectIds)
                {
                    links.Add(ProjectTeam.Link(organizationId, projectId, created.Id, currentUser.SubjectId, now));
                }

                foreach (var userId in userIds)
                {
                    teamMemberships.Add(TeamMembership.Add(created.Id, organizationId, userId));
                }

                // 6. One flush. A concurrent creation of the same name that committed first fails the insert on the team index; a
                // member removed from the organization meanwhile fails the composite key; each answers with its pre-check's code.
                await teams.SaveAsync(ct);

                return new AddTeamResult(new TeamResponse(created.Id, created.Name, created.Description, departmentId, department.Name, created.Color));
            },
            cancellationToken);
    }
}
