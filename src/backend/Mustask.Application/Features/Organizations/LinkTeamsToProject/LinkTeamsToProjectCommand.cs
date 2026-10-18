namespace Mustask.Application.Features.Organizations.LinkTeamsToProject;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/teams</c> (feat-028 api-contract § 2.4, US-3: link teams to the
/// project, from the project's page). Ids are the untrusted strings of the address and the body: the handler parses them with
/// <c>AddressedId</c>, so a malformed id gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="ProjectId">The project's id as it arrived in the address.</param>
/// <param name="TeamIds">
/// The teams to link, as sent. Absent, <c>null</c> and empty are the same: nothing to link, a no-op 204 (D-14); duplicates collapse
/// (api-contract § 0.2).
/// </param>
public sealed record LinkTeamsToProjectCommand(string? OrganizationId, string? ProjectId, IReadOnlyList<string?>? TeamIds);
