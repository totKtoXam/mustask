using Mustask.Application.Abstractions.Boards;

namespace Mustask.Application.Features.Boards.GetBacklog;

/// <summary>
/// The team's backlog (mod-006 api-contract § 3.1, feat-040 api-contract § 3.1): the team, <paramref name="Projects"/> — the
/// team's linked active projects, the filter's options (never narrowed by the filter) — the level, and the top-level rows in
/// the team's order — each with its «Проект», its «Родитель», its planning values and the whole tree beneath it (feat-027
/// api-contract § 2).
/// </summary>
public sealed record BacklogResponse(
    BoardTeamView Team,
    IReadOnlyList<BoardProjectView> Projects,
    BoardLevelView Level,
    IReadOnlyList<BacklogRowView> Items);
