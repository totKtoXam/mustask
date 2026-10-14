using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.WorkItems;

namespace Mustask.Application.Features.Boards.GetBoard;

/// <summary>The board (api-contract § 3.3): the team, the level, and its columns in board order with their cards.</summary>
public sealed record BoardResponse(BoardTeamView Team, BoardLevelView Level, IReadOnlyList<BoardColumnView> Columns);

/// <summary>A column: its key, header and category, what a drop writes per type, and its cards in the backlog's order, each with its checklist (feat-032).</summary>
public sealed record BoardColumnView(
    string Key,
    string Header,
    WorkItemStateCategoryView Category,
    IReadOnlyList<BoardColumnTargetView> Targets,
    IReadOnlyList<BoardCardView> Items);

/// <summary>The state a drop of a card of <see cref="TypeId"/> writes; <c>null</c>: no drop target (BR-M4-18).</summary>
public sealed record BoardColumnTargetView(Guid TypeId, Guid? StateId);
