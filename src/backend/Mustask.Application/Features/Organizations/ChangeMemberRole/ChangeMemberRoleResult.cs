using Mustask.Application.Abstractions.Organizations;

namespace Mustask.Application.Features.Organizations.ChangeMemberRole;

/// <summary>The handler's outcome: the member after the change, or unchanged when the role was already theirs (api-contract § 1.5).</summary>
public sealed record ChangeMemberRoleResult(MemberView Member);
