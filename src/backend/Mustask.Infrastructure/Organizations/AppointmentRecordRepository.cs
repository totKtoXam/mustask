using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;

namespace Mustask.Infrastructure.Organizations;

/// <summary>Adds to the request's context only; the membership repository's save flushes it with the membership (feat-033 plan § 4.2).</summary>
internal sealed class AppointmentRecordRepository(MustaskDbContext context) : IAppointmentRecordRepository
{
    public void Add(OrganizationAppointment appointment) => context.OrganizationAppointments.Add(appointment);
}
