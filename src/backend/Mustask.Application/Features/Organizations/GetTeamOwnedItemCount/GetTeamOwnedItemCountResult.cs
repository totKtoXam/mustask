namespace Mustask.Application.Features.Organizations.GetTeamOwnedItemCount;

/// <summary>The handler's outcome: <c>{ "count" }</c>, the number of items the team owns now, in any state (api-contract § 2.9).</summary>
public sealed record GetTeamOwnedItemCountResult(int Count);
