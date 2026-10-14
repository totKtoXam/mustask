using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.AddTeamMember;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/members</c> (api-contract § 2.3; US-3
/// "An administrator adds an organization member to a team", "Only active organization members are offered and accepted",
/// "A user is in a team at most once", "A team member added while the organization membership is removed", "A member
/// added to a team that is being deleted").
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.1, § 2.3), one numbered step each below: the address from the outermost
/// level in (the organization, the project, then the team under its lock), the value of the body (<c>userId</c>, 400), the
/// reference it names (the user must be an active member of this organization: <c>member_not_in_organization</c>), the
/// uniqueness pre-check (<c>member_exists</c>), and the save.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 3.8; ADR-0026 rule 8): the <b>team <c>FOR SHARE</c></b>, re-checked <c>Active</c> after the
/// lock — the mechanism every write that binds to a team uses. The team's deletion holds it <c>FOR UPDATE</c> and ends the
/// memberships under it: deletion first, this add answers <see cref="OrganizationErrorCodes.TeamNotFound"/>; add first, the
/// deletion removes the membership — in no order does a membership of a deleted team remain. The organization row is never
/// taken here. The composite key onto the organization membership is the second guard (BR-M1-10): the insert takes
/// <c>FOR KEY SHARE</c> on the organization membership, so a removal of that member waits for it and then cascades the new
/// row away, and one that committed first fails the insert, which the repository answers with
/// <c>member_not_in_organization</c>. The primary key <c>(team_id, user_id)</c> closes the duplicate add.
/// </para>
/// <para>
/// The user's role is unchanged and nothing is granted: team membership carries no code (doc-006 § Roles).
/// </para>
/// </remarks>
public sealed class AddTeamMemberHandler(
    IProjectRepository projects,
    ITeamRepository teams,
    ITeamMembershipRepository teamMemberships,
    IMemberReadModel members,
    IUserDirectory directory,
    IUnitOfWork unitOfWork)
{
    public async Task<AddTeamMemberResult> Handle(AddTeamMemberCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address, from the outermost level in.
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                if (!AddressedId.TryParse(command.ProjectId, out var projectId)
                    || !await projects.IsReachableAsync(organizationId, projectId, ct))
                {
                    throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
                }

                if (!AddressedId.TryParse(command.TeamId, out var teamId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                }

                // FOR SHARE, and the team is re-checked after the lock: a deletion that committed first answers not found.
                var team = await teams.LockForShareAsync(projectId, teamId, ct);

                if (team is not { IsActive: true })
                {
                    throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                }

                // 2. Rules about the addressed team alone: none.

                // 3. The value in the body.
                if (!OrganizationMembership.IsValidUserId(command.UserId))
                {
                    throw new ValidationException(
                        OrganizationErrorCodes.ValidationFailed,
                        new Dictionary<string, IReadOnlyList<FieldError>>
                        {
                            ["userId"] = [new FieldError(OrganizationErrorCodes.MembershipUserInvalid)],
                        });
                }

                var userId = command.UserId;

                // 4. The reference in the body: an active member of this organization, known to the projection (BR-M1-10,
                // BR-M1-32). Another organization's user, a projected non-member, an inactive member and an unknown id all
                // answer alike.
                var lookup = await directory.LookupAsync([userId], ct);

                if (!lookup.Known.TryGetValue(userId, out var user)
                    || !user.IsActive
                    || !await members.IsMemberAsync(organizationId, userId, ct))
                {
                    throw new ConflictException(OrganizationErrorCodes.TeamMemberNotInOrganization);
                }

                // 5. The uniqueness pre-check: a user is in a team at most once.
                if (await teamMemberships.ExistsAsync(team.Id, userId, ct))
                {
                    throw new ConflictException(OrganizationErrorCodes.TeamMemberExists);
                }

                // 6. Save. A concurrent add that committed first fails this insert on the primary key; the organization
                // membership removed meanwhile fails it on the composite key. The repository answers both with the
                // pre-checks' codes.
                teamMemberships.Add(TeamMembership.Add(team.Id, organizationId, userId));
                await teamMemberships.SaveAsync(ct);

                return new AddTeamMemberResult(UserRefView.From(user));
            },
            cancellationToken);
}
