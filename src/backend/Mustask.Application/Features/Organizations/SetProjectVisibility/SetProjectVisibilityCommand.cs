namespace Mustask.Application.Features.Organizations.SetProjectVisibility;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/visibility</c> (feat-041 api-contract § 2.1): the project's
/// «Видим всей организации». Ids are the untrusted strings of the address.
/// </summary>
/// <param name="VisibleToOrganization">The value; absent or not a boolean is refused 400 <c>visibleToOrganization</c>.</param>
public sealed record SetProjectVisibilityCommand(string? OrganizationId, string? ProjectId, bool? VisibleToOrganization);

/// <summary>The project's flag after the write.</summary>
public sealed record SetProjectVisibilityResponse(Guid Id, Guid OrganizationId, bool VisibleToOrganization);
