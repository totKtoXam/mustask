namespace Mustask.Application.Features.Organizations.GetAppointments;

/// <summary><c>GET /api/organizations/{organizationId}/appointments</c> (feat-033 api-contract § 2.1).</summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
public sealed record GetAppointmentsQuery(string? OrganizationId);
