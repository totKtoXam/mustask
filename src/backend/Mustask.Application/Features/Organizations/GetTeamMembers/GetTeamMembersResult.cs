using Mustask.Application.Abstractions.Organizations;

namespace Mustask.Application.Features.Organizations.GetTeamMembers;

/// <summary>The team's members as the server orders them (api-contract § 0.5). A team may have none.</summary>
public sealed record GetTeamMembersResult(IReadOnlyList<UserRefView> Items);
