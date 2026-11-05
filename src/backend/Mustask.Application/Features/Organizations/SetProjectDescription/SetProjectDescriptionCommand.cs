namespace Mustask.Application.Features.Organizations.SetProjectDescription;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/description</c> (feat-028 api-contract § 2.4, US-2: describe a
/// project). Ids are the untrusted strings of the address: a malformed one gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="ProjectId">The project's id as it arrived in the address.</param>
/// <param name="Description">
/// The description as sent, never trimmed. <c>null</c> (also <c>{}</c> and no body) and the empty string clear it (BR-OL-3).
/// </param>
public sealed record SetProjectDescriptionCommand(string? OrganizationId, string? ProjectId, string? Description);
