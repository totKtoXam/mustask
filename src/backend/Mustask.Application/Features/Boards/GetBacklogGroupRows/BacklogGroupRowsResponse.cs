using Mustask.Application.Abstractions.Boards;

namespace Mustask.Application.Features.Boards.GetBacklogGroupRows;

/// <summary>A page of a group: <c>nextCursor</c> is absent at the end; there is no total and no page number (ADR-0036).</summary>
public sealed record BacklogGroupRowsResponse(IReadOnlyList<AggregateItemView> Rows, string? NextCursor);
