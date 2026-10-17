namespace Mustask.Application.Features.Organizations.GetTeam;

/// <summary><c>Team</c> of mod-017 api-contract § 1.3, the one shape every team response uses. The slice's own copy.</summary>
/// <param name="DefaultMark"><c>named</c>, <c>onlyTeam</c> or <c>null</c> (BR-OT-4); there is no first team and no default area.</param>
/// <param name="IsMember">Always <c>false</c>: the single-team read does not carry the caller's membership (api-contract § 1.3).</param>
public sealed record TeamResponse(Guid Id, Guid ProjectId, string Name, string? DefaultMark, bool IsMember = false);
