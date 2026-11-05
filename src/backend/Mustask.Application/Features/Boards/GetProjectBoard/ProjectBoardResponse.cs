using Mustask.Application.Abstractions.Boards;

namespace Mustask.Application.Features.Boards.GetProjectBoard;

/// <summary>
/// The project's board (feat-039 api-contract § 3.3): the project, the level, and its columns in board order with
/// their cards, in the project's order. The column and card views are the team board's.
/// </summary>
public sealed record ProjectBoardResponse(BoardProjectView Project, BoardLevelView Level, IReadOnlyList<BoardColumnView> Columns);
