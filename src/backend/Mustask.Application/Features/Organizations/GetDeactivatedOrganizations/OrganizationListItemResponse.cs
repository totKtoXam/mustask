namespace Mustask.Application.Features.Organizations.GetDeactivatedOrganizations;

/// <summary><c>OrganizationListItem</c> of api-contract § 1.1. The slice's own copy of the shape.</summary>
public sealed record OrganizationListItemResponse(Guid Id, string Name);
