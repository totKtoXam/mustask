namespace Mustask.Application.Features.Organizations.GetTeams;

/// <summary><c>Team</c> of mod-017 api-contract § 1.3, the one shape every team response uses. The slice's own copy.</summary>
/// <param name="DefaultMark">
/// <c>named</c>: the project's settings name this team; <c>onlyTeam</c>: no team is named and this is the project's only active
/// team; <c>null</c>: neither (BR-OT-4). At most one team of a project carries a mark. There is no first team and a team has no
/// default area (BR-OT-10, BR-OT-11).
/// </param>
/// <param name="IsMember">
/// The <b>caller</b> is in this team (mod-016 api-contract § 3.3): the panel opens the project on the first team of the
/// list's order (by name) with this flag set, else on the first team. Only the list carries it.
/// </param>
public sealed record TeamResponse(Guid Id, Guid ProjectId, string Name, string? DefaultMark, bool IsMember = false);
