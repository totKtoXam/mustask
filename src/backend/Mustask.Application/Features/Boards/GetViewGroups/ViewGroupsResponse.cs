using Mustask.Application.Abstractions.Boards;

namespace Mustask.Application.Features.Boards.GetViewGroups;

/// <summary>The groups of a view in the order of BR-AV-4 (no department or no team first), the empty ones absent; a board's carry their column counts.</summary>
public sealed record ViewGroupsResponse(string Grouping, IReadOnlyList<GroupHeaderView> Groups);
