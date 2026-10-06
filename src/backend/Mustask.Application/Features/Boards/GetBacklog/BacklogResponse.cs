using Mustask.Application.Abstractions.Boards;

namespace Mustask.Application.Features.Boards.GetBacklog;

/// <summary>The backlog (api-contract § 3.1): the team, the level, and every item in the backlog's order.</summary>
public sealed record BacklogResponse(BoardTeamView Team, BoardLevelView Level, IReadOnlyList<BacklogItemView> Items);
