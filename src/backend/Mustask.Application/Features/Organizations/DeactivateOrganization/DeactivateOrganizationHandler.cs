using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.DeactivateOrganization;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/deactivate</c> (US-2 "The
/// organization is deactivated", api-contract § 1.6): the organization leaves
/// the active list and joins the deactivated one, and everything beneath it
/// answers not found through reachability. The handler writes this one row:
/// nothing beneath it is read, changed or deleted (BR-M1-20, ADR-0026 rules 2
/// and 8).
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3): the address is the only
/// level, and the only rule. The scoped check has answered for
/// a malformed, unknown or already deactivated organization before the handler
/// runs; the handler asks again where it writes the organization itself,
/// because the filter's read is not under the lock: a second deactivation that
/// waited for the first one's commit must find the organization deactivated and
/// answer <see cref="OrganizationErrorCodes.OrganizationNotFound"/> too. There
/// is no body, no reference and no uniqueness rule, so nothing else can refuse
/// the request.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 8.2): the organization <c>FOR NO KEY UPDATE</c>,
/// the lock of every rename, deactivation and restore of it. A write under the
/// organization takes no lock on it and only reads its status, so it is always
/// serializable against this one (plan-backend § 8.1).
/// </para>
/// </remarks>
public sealed class DeactivateOrganizationHandler(IOrganizationRepository organizations, IUnitOfWork unitOfWork)
{
    public async Task Handle(DeactivateOrganizationCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address. A malformed id, an unknown organization, and one that is not active alike answer
                // organization.not_found, a deactivated one included (api-contract § 1.6). The lock is taken first and the row is
                // read after it, so a concurrent change that committed first is seen.
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                var organization = await organizations.LockAsync(organizationId, ct);

                if (organization is not { IsActive: true })
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                // 2-5. Rules about the addressed object, the body, its references and uniqueness: none.

                // 6. Save: one row. Projects, teams, areas and the process are not touched (BR-M1-20).
                organization.Deactivate();
                await organizations.SaveAsync(ct);

                return true;
            },
            cancellationToken);
}
