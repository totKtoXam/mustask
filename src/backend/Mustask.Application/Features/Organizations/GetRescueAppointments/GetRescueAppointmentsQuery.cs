namespace Mustask.Application.Features.Organizations.GetRescueAppointments;

/// <summary><c>GET /api/organizations/rescue/{organizationId}/appointments</c> (feat-033 api-contract § 1.4).</summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
public sealed record GetRescueAppointmentsQuery(string? OrganizationId);
