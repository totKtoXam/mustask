using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetRescueAppointments;

/// <summary>
/// <c>GET /api/organizations/rescue/{organizationId}/appointments</c> (feat-033 api-contract § 1.4; US-3): the record of any
/// organization, active or deactivated, newest first. The instance policy has answered 401 and 403; the address ends at the
/// organization, which is found or not (404).
/// </summary>
public sealed class GetRescueAppointmentsHandler(IAppointmentReadModel appointments)
{
    public async Task<IReadOnlyList<AppointmentView>> Handle(GetRescueAppointmentsQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId)
            || !await appointments.OrganizationExistsAsync(organizationId, cancellationToken))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        return await appointments.ListAsync(organizationId, cancellationToken);
    }
}
