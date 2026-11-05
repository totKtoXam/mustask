using Mustask.Application.Abstractions.Organizations;

namespace Mustask.Application.Features.Organizations.AppointAdministrator;

/// <summary>The handler's outcome: the record just written, as <c>GET …/appointments</c> shows it (api-contract § 1.3).</summary>
public sealed record AppointAdministratorResult(AppointmentView Appointment);
