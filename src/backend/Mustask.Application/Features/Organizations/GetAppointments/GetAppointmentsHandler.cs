using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetAppointments;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/appointments</c> (feat-033 api-contract § 2.1; US-3 "The organization's
/// administrators read their own record"): the same rows as the system administrator's read, for the organization's own
/// administrators. The scoped check has answered 401, 404 and 403 already; the handler parses the id again only to name it.
/// </summary>
public sealed class GetAppointmentsHandler(IAppointmentReadModel appointments)
{
    public async Task<IReadOnlyList<AppointmentView>> Handle(GetAppointmentsQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        return await appointments.ListAsync(organizationId, cancellationToken);
    }
}
