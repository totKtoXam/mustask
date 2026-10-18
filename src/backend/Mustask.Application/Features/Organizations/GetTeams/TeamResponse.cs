namespace Mustask.Application.Features.Organizations.GetTeams;

/// <summary><c>ProjectTeam</c> of feat-028 api-contract § 1.3, the shape of a project's list of linked teams. The slice's own copy.</summary>
/// <param name="DefaultMark">
/// <c>named</c>: the project's settings name this team; <c>onlyTeam</c>: no team is named and this is the project's only linked
/// team; <c>null</c>: neither (BR-OL-7). At most one team of a project carries a mark.
/// </param>
/// <param name="IsMember">
/// The <b>caller</b> is in this team (mod-016 api-contract § 3.3): the panel opens the project on the first team of the
/// list's order (by name) with this flag set, else on the first team. Only the list carries it.
/// </param>
public sealed record TeamResponse(Guid Id, string Name, string? DefaultMark, bool IsMember = false);
