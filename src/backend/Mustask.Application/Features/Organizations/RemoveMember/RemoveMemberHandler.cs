using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.RemoveMember;

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/members/{userId}</c> (api-contract § 1.6; US-1 "An administrator removes a
/// member", "An administrator removes themselves while another admin remains", "Removing a membership that is already gone").
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.1, § 1.6): the address (the organization, then the member in the path:
/// <c>membership.not_found</c>); the request has no body, so there are no values or references to answer; then, inside one
/// transaction, the rule about the member (<c>last_admin</c>, 409), the caller's role read again (<c>access.forbidden</c>,
/// 403, only for a caller demoted while the request waited) and the delete. The scoped check has answered 401, 404 and 403
/// already.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 3.8, P-4): the same sequence as <c>ChangeMemberRole</c> — the organization row
/// <c>FOR NO KEY UPDATE</c>, the caller's role read again under it (gone: answered as a non-member, api-contract § 1.6), the
/// member's row <c>FOR UPDATE</c>. Two administrators who remove each other queue on the organization row, and the second
/// finds its own caller gone or counts one administrator fewer: exactly one removal is saved.
/// An <b>inactive</b> administrator is removed only while a known active one remains (BR-M1-4, BR-M1-32).
/// </para>
/// <para>
/// <b>What the second look at the caller decides</b> (plan-backend § 3.6 rows 5 and 6, review round 1a BE-SEC-1): the scoped
/// check admitted the request at its start, outside this transaction, and an administrator whose demotion committed while it
/// waited for the lock is no longer one. The rule of BR-M1-4 answers first (<c>last_admin</c>, so two administrators who
/// demote and remove each other keep api-contract § 1.6's answer), then the caller's role must still hold
/// <c>organizations.membership.write</c>, else 403 <c>access.forbidden</c> and nothing is deleted.
/// </para>
/// <para>
/// <b>What the delete ends.</b> One raw <c>DELETE</c>: the composite key from <c>team_memberships</c> cascades, so the user
/// leaves every team of the organization in the same statement (BR-M1-36). Work items assigned to them keep their assignee
/// — nothing here touches them (BR-M1-26). The caller may remove themselves; their next request answers 404.
/// </para>
/// </remarks>
public sealed class RemoveMemberHandler(
    IOrganizationRepository organizations,
    IMembershipRepository memberships,
    IMemberReadModel members,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task Handle(RemoveMemberCommand command, CancellationToken cancellationToken)
    {
        // The unit of work returns a result; this use case has none (204), so it returns a flag nobody reads.
        _ = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address.
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                if (!OrganizationMembership.IsValidUserId(command.UserId)
                    || !await memberships.ExistsAsync(organizationId, command.UserId, ct))
                {
                    throw new NotFoundException(OrganizationErrorCodes.MembershipNotFound);
                }

                var userId = command.UserId;

                // 2., 3. and 4. Rules, values and references: none before the locks, and no body.

                // The locks, organization first (see the remarks).
                var organization = await organizations.LockAsync(organizationId, ct);

                if (organization is not { IsActive: true })
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                var actorRole = await memberships.FindRoleAsync(organizationId, currentUser.SubjectId, ct)
                    ?? throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);

                var target = await memberships.LockMemberAsync(organizationId, userId, ct)
                    ?? throw new NotFoundException(OrganizationErrorCodes.MembershipNotFound);

                // 5. BR-M1-4: removing an administrator, an inactive or unknown one included, must leave a known active one. It
                // answers before the caller's role is judged (api-contract § 1.6: of two administrators who demote and remove
                // each other the loser hears last_admin).
                if (target.IsAdmin)
                {
                    OrganizationAdminRule.EnsureAnAdministratorRemains(
                        await members.CountOtherKnownActiveAdministratorsAsync(organizationId, userId, ct));
                }

                // The caller, read again, still holds the code (BE-SEC-1): the scoped check judged the role they had when the
                // request began, not the one a demotion that committed meanwhile has left them.
                if (!OrganizationRoleBundles.Holds(actorRole, PermissionCatalog.OrganizationsMembershipWrite))
                {
                    throw new ForbiddenException(OrganizationErrorCodes.AccessForbidden);
                }

                // 6. Delete: the cascade ends the user's team memberships in the same statement (BR-M1-36).
                if (!await memberships.DeleteAsync(organizationId, userId, ct))
                {
                    throw new NotFoundException(OrganizationErrorCodes.MembershipNotFound);
                }

                return true;
            },
            cancellationToken);
    }
}
