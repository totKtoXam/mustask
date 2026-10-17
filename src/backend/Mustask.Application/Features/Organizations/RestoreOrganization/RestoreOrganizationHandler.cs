using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.RestoreOrganization;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/restore</c> (US-2 "The organization is
/// restored exactly as it was"). It is the one request a deactivated organization
/// accepts, so the scoped check lets a deactivated organization
/// through on this route only (plan-backend § 7.3).
/// </summary>
/// <remarks>
/// <para>
/// <b>What it writes.</b> One row: the organization's own status (ADR-0026 rule 2).
/// Nothing beneath is read or changed, so a project that was deactivated on its
/// own before the organization stays deactivated, and the teams, the default-team setting
/// and the area trees come back through reachability alone, as they were
/// (BR-M1-20). Restoring an active organization changes nothing and answers the
/// same (api-contract § 1.7).
/// </para>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3): the address is the only fault
/// there can be, because the route has no body. The filter has answered for a
/// malformed or an unknown id already; the handler re-checks it where it writes
/// the organization itself.
/// </para>
/// <para>
/// <b>Lock</b> (plan-backend § 8.2): the organization <c>FOR NO KEY UPDATE</c>, of
/// any status, as the rename and the deactivation take it, so the three serialize
/// on the row. No unique index is touched: the name stays reserved for good while
/// the organization is deactivated (BR-M1-1), so a restore can never collide, and
/// there is no race to map.
/// </para>
/// </remarks>
public sealed class RestoreOrganizationHandler(IOrganizationRepository organizations, IUnitOfWork unitOfWork)
{
    public async Task<RestoreOrganizationResult> Handle(RestoreOrganizationCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address: a malformed, unknown or (when the filter is bypassed) missing organization is not found.
                // The lock takes an organization of any status, because a deactivated one is exactly what is addressed.
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                var organization = await organizations.LockAsync(organizationId, ct)
                    ?? throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);

                // 2..5. No rule about the object alone, no body, no reference, no uniqueness: a restore of an active
                // organization is a no-op in the domain (BR-M1-20).
                organization.Restore();

                // 6. Save. One row, no unique index involved.
                await organizations.SaveAsync(ct);

                return new RestoreOrganizationResult(new OrganizationResponse(organization.Id, organization.Name));
            },
            cancellationToken);
}
