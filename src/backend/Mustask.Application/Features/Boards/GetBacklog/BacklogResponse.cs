using Mustask.Application.Abstractions.Boards;

namespace Mustask.Application.Features.Boards.GetBacklog;

/// <summary>
/// The backlog (api-contract § 3.1): the team, the level, and every item in the
/// backlog's order — each with its «Родитель» and the rows nested beneath it
/// (feat-001 api-contract § 4).
/// </summary>
public sealed record BacklogResponse(BoardTeamView Team, BoardLevelView Level, IReadOnlyList<BacklogRowView> Items);
