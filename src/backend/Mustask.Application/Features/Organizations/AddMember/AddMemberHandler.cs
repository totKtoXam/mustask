using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.AddMember;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/members</c> (api-contract § 1.4; US-1 "An administrator adds a member from
/// «Участники»", "A role outside the three is refused", "An inactive or unknown user is neither offered nor added").
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.1, § 1.4), one numbered step each below. The scoped check has answered
/// 401, 404 and 403 already, before the body was read: this handler is the reference the other membership slices copy
/// the shape of. Then the address, no rule about the addressed organization alone, the values of the body (<c>userId</c>
/// and <c>role</c>, every reason at once), the reference the body names (the user, through the projection: known and
/// active, else <c>user_unavailable</c>), the uniqueness pre-check (<c>already_member</c> — answered after
/// <c>user_unavailable</c>), and the save.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 3.8): none, and no transaction. Adding a member cannot threaten BR-M1-4, and the primary
/// key <c>(organization_id, user_id)</c> closes BR-M1-5 (ADR-0011 echelon 1): the pre-check is advisory, and a concurrent
/// add that committed first fails this insert, which the repository answers with the pre-check's code.
/// </para>
/// <para>
/// <b>The projection, not Keycloak.</b> A user the projection does not know yet (mod-014 BR-UP-17) or whose account is
/// disabled cannot be added (BR-M1-32). The check is the add operation's alone: access checks read the membership and
/// never the projection. The membership records the caller as <c>addedBy</c> and the time; it applies to the user's next
/// request, because access is read from the database on every request (BR-M1-6).
/// </para>
/// </remarks>
public sealed class AddMemberHandler(
    IMembershipRepository memberships,
    IMemberReadModel members,
    IUserDirectory directory,
    IClock clock,
    ICurrentUser currentUser)
{
    public async Task<AddMemberResult> Handle(AddMemberCommand command, CancellationToken cancellationToken)
    {
        // 1. The address. The scoped check has answered for the organization; the handler parses it again to name it.
        if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        // 2. Rules about the addressed organization alone: none, a create addresses only its container.

        // 3. The values in the body: one ValidationException with every field reason at once.
        var errors = new Dictionary<string, IReadOnlyList<FieldError>>();

        if (!OrganizationMembership.IsValidUserId(command.UserId))
        {
            errors["userId"] = [new FieldError(OrganizationErrorCodes.MembershipUserInvalid)];
        }

        if (!OrganizationRoles.TryParse(command.Role, out var role))
        {
            errors["role"] = [new FieldError(OrganizationErrorCodes.MembershipRoleInvalid)];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(OrganizationErrorCodes.ValidationFailed, errors);
        }

        // 4. The reference in the body: the user, known to the projection and active (BR-M1-32). Nothing is created otherwise.
        var userId = command.UserId!;
        var lookup = await directory.LookupAsync([userId], cancellationToken);

        if (!lookup.Known.TryGetValue(userId, out var user) || !user.IsActive)
        {
            throw new ConflictException(OrganizationErrorCodes.MembershipUserUnavailable);
        }

        // 5. The uniqueness pre-check (BR-M1-5), after the user's own refusal.
        if (await memberships.ExistsAsync(organizationId, userId, cancellationToken))
        {
            throw new ConflictException(OrganizationErrorCodes.MembershipAlreadyMember);
        }

        // 6. Save. A concurrent add that committed first fails this insert on the primary key, and the repository answers
        // it with the pre-check's code.
        memberships.Add(OrganizationMembership.Add(organizationId, userId, role, currentUser.SubjectId, clock.UtcNow));
        await memberships.SaveAsync(cancellationToken);

        var member = await members.FindAsync(organizationId, userId, cancellationToken)
            ?? throw new InvalidOperationException($"The membership of {userId} was saved but cannot be read back.");

        return new AddMemberResult(member);
    }
}
