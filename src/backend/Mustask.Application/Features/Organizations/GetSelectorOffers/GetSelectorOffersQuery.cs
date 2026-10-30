namespace Mustask.Application.Features.Organizations.GetSelectorOffers;

/// <summary><c>GET /api/organizations/{organizationId}/selector-offers</c> (feat-043 US-2, US-8). The id is the untrusted string of the address (BR-M1-20).</summary>
public sealed record GetSelectorOffersQuery(string? OrganizationId);
