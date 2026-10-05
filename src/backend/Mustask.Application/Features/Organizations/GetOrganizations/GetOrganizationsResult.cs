namespace Mustask.Application.Features.Organizations.GetOrganizations;

/// <summary>The list as the server orders it; the client renders it in the order received (api-contract § 0.6).</summary>
public sealed record GetOrganizationsResult(IReadOnlyList<OrganizationListItemResponse> Items);
