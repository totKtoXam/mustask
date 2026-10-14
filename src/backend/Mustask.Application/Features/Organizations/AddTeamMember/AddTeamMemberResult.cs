using Mustask.Application.Abstractions.Organizations;

namespace Mustask.Application.Features.Organizations.AddTeamMember;

/// <summary>The handler's outcome: the new team member as a user reference (api-contract § 2.3). Their role is unchanged.</summary>
public sealed record AddTeamMemberResult(UserRefView Member);
