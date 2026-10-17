namespace Mustask.Application.Features.Organizations.RenameTeam;

/// <summary><c>Team</c> of mod-017 api-contract § 1.3, the one shape every team response uses. The slice's own copy.</summary>
/// <param name="DefaultMark"><c>named</c>, <c>onlyTeam</c> or <c>null</c> (BR-OT-4).</param>
/// <param name="IsMember">Always <c>false</c>: the rename does not carry the caller's membership (api-contract § 1.3).</param>
public sealed record TeamResponse(Guid Id, Guid ProjectId, string Name, string? DefaultMark, bool IsMember = false);
