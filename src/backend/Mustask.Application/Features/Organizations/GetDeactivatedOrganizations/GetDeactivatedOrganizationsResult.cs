namespace Mustask.Application.Features.Organizations.GetDeactivatedOrganizations;

/// <summary>The list as the server orders it; the client renders it in the order received (api-contract § 0.6).</summary>
public sealed record GetDeactivatedOrganizationsResult(IReadOnlyList<OrganizationListItemResponse> Items);
