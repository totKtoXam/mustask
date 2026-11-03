namespace Mustask.Application.Features.Organizations.GetOverview;

/// <summary><c>GET /api/organizations/{organizationId}/overview</c> (feat-043 US-2): the organization's «Обзор». The id is the untrusted string of the address (BR-M1-20).</summary>
public sealed record GetOverviewQuery(string? OrganizationId);
