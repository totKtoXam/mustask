using Mustask.Application.Abstractions.Boards;

namespace Mustask.Application.Features.Boards.GetBoardCells;

/// <summary>A page of a cell's cards; no total (the lane's header counts it) and no page number (ADR-0036).</summary>
public sealed record BoardCellsResponse(IReadOnlyList<AggregateCardView> Cards, string? NextCursor);
