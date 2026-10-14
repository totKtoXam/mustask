using Mustask.Application.Abstractions.Organizations;

namespace Mustask.Application.Features.Organizations.GetMembers;

/// <summary>The members as the server orders them (api-contract § 0.5); the panel renders them in the order received.</summary>
public sealed record GetMembersResult(IReadOnlyList<MemberView> Items);
