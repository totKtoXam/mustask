using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.AppointAdministrator;

/// <summary>
/// <c>POST /api/organizations/rescue/{organizationId}/appointments</c> (feat-033 api-contract § 1.3; US-2, US-3): a system
/// administrator makes a user an administrator of any organization and leaves a record of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (BR-RS-6a), one numbered step each below. The instance policy has answered 401 and 403 before the
/// body was read. The caller is <b>not</b> a member of the organization and the handler asks <see cref="IOrganizationAccess"/>
/// nothing: the instance code is the whole guard (ADR-0029 § 9). A deactivated organization is accepted (BR-RS-4).
/// </para>
/// <para>
/// <b>Locks</b> (BR-RS-11; mod-016's order, ChangeMemberRole's plan § 3.8): the <b>organization row</b> <c>FOR NO KEY UPDATE</c>,
/// then the <b>user's membership row</b> <c>FOR UPDATE</c> if there is one. So the appointment queues behind a role change or a
/// removal of the same organization and behind another appointment, and the rules are judged on committed state. An add of
/// mod-016 takes no lock; one that commits between the lock and the insert fails the insert on the primary key, which the
/// repository answers with <c>membership.already_member</c> (D-1), and the transaction rolls back with no record.
/// </para>
/// <para>
/// <b>One transaction</b>: the membership change and the record are flushed by one <c>SaveChanges</c> — both or neither
/// (BR-RS-10). Nothing is written on a refusal, and no Keycloak call is made.
/// </para>
/// </remarks>
public sealed class AppointAdministratorHandler(
    IOrganizationRepository organizations,
    IMembershipRepository memberships,
    IAppointmentRecordRepository records,
    IAppointmentReadModel appointments,
    IUserDirectory directory,
    IClock clock,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<AppointAdministratorResult> Handle(AppointAdministratorCommand command, CancellationToken cancellationToken)
    {
        var recordId = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address.
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                // 2. The organization, locked, of any status.
                if (await organizations.LockAsync(organizationId, ct) is null)
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                // 3. The user in the body.
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

                // 4. The projection (a database read), then the user's membership row under the organization's lock.
                var lookup = await directory.LookupAsync([userId], ct);
                var appointable = lookup.Known.TryGetValue(userId, out var user) && user.IsActive;
                var target = await memberships.LockMemberAsync(organizationId, userId, ct);

                // 5. The refusals about the user, in their one order.
                var actorId = currentUser.SubjectId;
                var effect = OrganizationAppointmentRule.Decide(actorId, userId, appointable, target?.Role);

                // 6. The reason.
                if (!AppointmentReason.TryCreate(command.Reason, out var reason, out var reasonError))
                {
                    throw new ValidationException(
                        OrganizationErrorCodes.ValidationFailed,
                        new Dictionary<string, IReadOnlyList<FieldError>> { ["reason"] = [reasonError!] });
                }

                // 7. The change and its record, flushed together.
                var now = clock.UtcNow;

                if (effect == AppointmentEffect.Raise)
                {
                    target!.ChangeRole(OrganizationRole.Admin, actorId, now);
                }
                else
                {
                    memberships.Add(OrganizationMembership.Add(organizationId, userId, OrganizationRole.Admin, actorId, now));
                }

                var record = OrganizationAppointment.Record(organizationId, userId, actorId, reason, now);
                records.Add(record);
                await memberships.SaveAsync(ct);

                return record.Id;
            },
            cancellationToken);

        // 8. The record as the history shows it, read after the commit.
        return new AppointAdministratorResult(
            await appointments.FindAsync(recordId, cancellationToken)
            ?? throw new InvalidOperationException($"The appointment {recordId} was saved but cannot be read back."));
    }
}
