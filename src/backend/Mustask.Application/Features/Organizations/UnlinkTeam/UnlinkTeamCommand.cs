namespace Mustask.Application.Features.Organizations.UnlinkTeam;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/unlink</c> (feat-028 api-contract § 2.4, SC-09,
/// BR-OL-8): take a team off a project, giving the items it owns in that project to another team of the project, or to nobody.
/// Ids are the untrusted strings of the address and the body: the handler parses them with <c>AddressedId</c>, so a malformed id
/// gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="ProjectId">The project's id as it arrived in the address.</param>
/// <param name="TeamId">The unlinked team's id as it arrived in the address.</param>
/// <param name="Destination">
/// The <c>destination</c> wrapper of the body, or <c>null</c> when it is absent, <c>null</c> or <c>{}</c> — that is 400
/// <c>destination_required</c> (D-8): «Без команды» is never the answer to a missing key.
/// </param>
public sealed record UnlinkTeamCommand(string? OrganizationId, string? ProjectId, string? TeamId, UnlinkDestination? Destination);

/// <summary>
/// A destination whose <c>teamId</c> key was present (D-8): a string names the team that receives the items, and <c>null</c>
/// is «Без команды» — the items are left with no owning team.
/// </summary>
/// <param name="TeamId">The destination team as it arrived, or <c>null</c> for «Без команды».</param>
public sealed record UnlinkDestination(string? TeamId);
