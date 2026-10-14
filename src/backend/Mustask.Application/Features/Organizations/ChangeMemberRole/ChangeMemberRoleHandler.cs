using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.ChangeMemberRole;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/members/{userId}/role</c> (api-contract § 1.5; US-1 "An administrator changes
/// a member's role from the row menu", "A role outside the three is refused", "An unknown administrator does not count as
/// the one who remains", "Removing a membership that is already gone").
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.1, § 1.5), one numbered step each below: the address (the organization,
/// then the member in the path: <c>membership.not_found</c>), the value of the body (<c>role</c>, 400), then, inside one
/// transaction, the rules about the member and the organization (<c>last_admin</c>, 409) and the caller's role read again
/// (<c>access.forbidden</c>, 403, only for a caller demoted while the request waited). The scoped check has answered 401, 404
/// and 403 already.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 3.8, P-4), in this order: the <b>organization row</b> <c>FOR NO KEY UPDATE</c> — every
/// change or removal of a member takes it first, so two administrators who downgrade or remove each other queue, and the
/// second finds what the first committed — then the <b>caller's role is read again</b> under the lock (it may be gone:
/// answered as a non-member, api-contract § 1.6), then the target member's row <c>FOR UPDATE</c> (gone:
/// <c>membership.not_found</c>).
/// </para>
/// <para>
/// <b>What the second look at the caller decides</b> (plan-backend § 3.6 row 5, review round 1a BE-SEC-1). The scoped check
/// admitted the request at its start, outside this transaction; an administrator whose demotion committed while the request
/// waited for the lock is no longer one, and a change made in their name would let them undo the demotion or promote and
/// remove others. So, in this order: the rule of BR-M1-4 first — it counts the <i>other</i> administrators that are known and
/// active in the projection (BR-M1-32) on committed data, and when the change takes an administrator out of the bundle and
/// leaves none, it is refused with <c>last_admin</c> and nothing is written, so of two administrators who downgrade each
/// other exactly one change is saved and the other answers <c>last_admin</c> (api-contract § 1.6); then the caller's role
/// must still hold <c>organizations.membership.write</c>, else 403 <c>access.forbidden</c>. Setting the role the member
/// already has answers 200 and changes nothing, not even the two «changed» fields — for a caller who still holds the code.
/// </para>
/// </remarks>
public sealed class ChangeMemberRoleHandler(
    IOrganizationRepository organizations,
    IMembershipRepository memberships,
    IMemberReadModel members,
    IClock clock,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<ChangeMemberRoleResult> Handle(ChangeMemberRoleCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address: the organization, then the member in the path. A user id that is blank or too long names
                // no membership, like one that is merely unknown (api-contract § 0.3).
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

                // 2. Rules about the addressed membership alone: none before the locks.

                // 3. The value in the body.
                if (!OrganizationRoles.TryParse(command.Role, out var role))
                {
                    throw new ValidationException(
                        OrganizationErrorCodes.ValidationFailed,
                        new Dictionary<string, IReadOnlyList<FieldError>>
                        {
                            ["role"] = [new FieldError(OrganizationErrorCodes.MembershipRoleInvalid)],
                        });
                }

                // 4. The locks, organization first. An organization deactivated since the check is not found; the caller,
                // read again, may have been removed or demoted by an administrator who won the lock (plan-backend § 3.8).
                var organization = await organizations.LockAsync(organizationId, ct);

                if (organization is not { IsActive: true })
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                var actorId = currentUser.SubjectId;

                var actorRole = await memberships.FindRoleAsync(organizationId, actorId, ct)
                    ?? throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);

                var target = await memberships.LockMemberAsync(organizationId, userId, ct)
                    ?? throw new NotFoundException(OrganizationErrorCodes.MembershipNotFound);

                // 5. BR-M1-4: an administrator stepping down must leave a known active administrator behind. It answers before
                // the caller's role is judged, so that two administrators downgrading each other keep api-contract § 1.6's
                // answer (the loser hears last_admin) while a caller demoted by the winner and acting on anyone else is refused.
                if (target.IsAdmin && target.Role != role)
                {
                    OrganizationAdminRule.EnsureAnAdministratorRemains(
                        await members.CountOtherKnownActiveAdministratorsAsync(organizationId, userId, ct));
                }

                // 6. The caller, read again, still holds the code (BE-SEC-1): the scoped check judged the role they had when the
                // request began, not the one a demotion that committed meanwhile has left them.
                if (!OrganizationRoleBundles.Holds(actorRole, PermissionCatalog.OrganizationsMembershipWrite))
                {
                    throw new ForbiddenException(OrganizationErrorCodes.AccessForbidden);
                }

                // The role the member already has: 200, nothing changes, nothing is written.
                if (target.Role == role)
                {
                    return new ChangeMemberRoleResult(await ReadAsync(organizationId, userId, ct));
                }

                target.ChangeRole(role, actorId, clock.UtcNow);
                await memberships.SaveAsync(ct);

                return new ChangeMemberRoleResult(await ReadAsync(organizationId, userId, ct));
            },
            cancellationToken);

    private async Task<MemberView> ReadAsync(Guid organizationId, string userId, CancellationToken cancellationToken) =>
        await members.FindAsync(organizationId, userId, cancellationToken)
            ?? throw new NotFoundException(OrganizationErrorCodes.MembershipNotFound);
}
