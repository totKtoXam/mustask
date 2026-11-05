using Mustask.Application.Abstractions.Organizations;

namespace Mustask.Application.Features.Organizations.AddMember;

/// <summary>The handler's outcome: the new member, as <c>GET …/members</c> shows them (api-contract § 1.4).</summary>
public sealed record AddMemberResult(MemberView Member);
