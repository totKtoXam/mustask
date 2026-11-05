using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// The write side of the appointment record (feat-033 BR-RS-10): <b>insert only</b> — there is no update and no delete, so
/// nothing can edit or remove a record. It adds to the request's context, so <see cref="IMembershipRepository.SaveAsync"/>
/// flushes the membership and the record in one <c>SaveChanges</c> inside the handler's transaction: both or neither.
/// </summary>
public interface IAppointmentRecordRepository
{
    void Add(OrganizationAppointment appointment);
}
