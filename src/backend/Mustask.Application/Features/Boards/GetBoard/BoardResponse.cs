using Mustask.Application.Abstractions.Boards;

namespace Mustask.Application.Features.Boards.GetBoard;

/// <summary>
/// The board (api-contract § 3.3): the team, the level, and its columns in board order with their cards. The column
/// and target views are shared with the project's board (feat-039), so they live in <c>Abstractions/Boards</c>.
/// </summary>
public sealed record BoardResponse(BoardTeamView Team, BoardLevelView Level, IReadOnlyList<BoardColumnView> Columns);
