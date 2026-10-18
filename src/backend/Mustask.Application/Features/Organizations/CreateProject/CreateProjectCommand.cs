namespace Mustask.Application.Features.Organizations.CreateProject;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects</c> (US-3: create a project with its root; feat-028 BR-OL-2 — no team comes
/// with it — and its optional description and teams). Ids are the untrusted strings of the address and the body: the handler parses
/// them with <c>AddressedId</c>, so a malformed id gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="Name">The name as sent, untrimmed.</param>
/// <param name="Key">The key as sent: never trimmed or upper-cased.</param>
/// <param name="Description">The description as sent, never trimmed; <c>null</c>, absent and the empty string are none (BR-OL-3).</param>
/// <param name="TeamIds">
/// The teams to link the project to, as sent: each must be an active team of this organization. Absent and <c>null</c> are empty;
/// duplicates collapse. Two teams chosen name no default (BR-OL-7).
/// </param>
public sealed record CreateProjectCommand(
    string? OrganizationId,
    string? Name,
    string? Key,
    string? Description = null,
    IReadOnlyList<string?>? TeamIds = null);
