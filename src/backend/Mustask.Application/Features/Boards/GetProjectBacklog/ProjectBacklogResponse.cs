using Mustask.Application.Abstractions.Boards;

namespace Mustask.Application.Features.Boards.GetProjectBacklog;

/// <summary>
/// The project's backlog (feat-039 api-contract § 3.1): the project, the level, and the top-level rows in the
/// project's order — each with its «Родитель», its planning values and the whole tree beneath it (feat-027
/// api-contract § 2), over every item of the project whatever its team.
/// </summary>
public sealed record ProjectBacklogResponse(BoardProjectView Project, BoardLevelView Level, IReadOnlyList<BacklogRowView> Items);
