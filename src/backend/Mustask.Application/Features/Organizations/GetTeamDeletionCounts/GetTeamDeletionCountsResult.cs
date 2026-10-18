namespace Mustask.Application.Features.Organizations.GetTeamDeletionCounts;

/// <summary><c>DeletionCounts</c> of feat-028 api-contract § 1.5: every project the team is linked to, deactivated ones and zeros included.</summary>
public sealed record GetTeamDeletionCountsResult(IReadOnlyList<ProjectDeletionCount> Projects);

/// <summary>The items the team owns in one project, in any state, when the read ran.</summary>
public sealed record ProjectDeletionCount(Guid ProjectId, string Name, bool Active, int Count);
