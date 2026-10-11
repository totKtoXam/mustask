using Mustask.Application.Abstractions.Boards;

namespace Mustask.Application.Features.Boards.GetBacklog;

/// <summary>
/// The backlog (mod-006 api-contract § 3.1): the team, the level, and the
/// top-level rows in the backlog's order — each with its «Родитель», its
/// planning values and the whole tree beneath it (feat-027 api-contract § 2).
/// </summary>
public sealed record BacklogResponse(BoardTeamView Team, BoardLevelView Level, IReadOnlyList<BacklogRowView> Items);
