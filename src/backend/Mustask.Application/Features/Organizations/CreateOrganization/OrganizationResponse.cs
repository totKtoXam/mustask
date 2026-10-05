namespace Mustask.Application.Features.Organizations.CreateOrganization;

/// <summary>
/// <c>Organization</c> of api-contract § 1.3: the organization as stored. Slices
/// do not reference each other (<c>VerticalSliceRules</c>), so this is the
/// slice's own copy of the shape.
/// </summary>
/// <param name="Name">As stored: trimmed.</param>
public sealed record OrganizationResponse(Guid Id, string Name);
