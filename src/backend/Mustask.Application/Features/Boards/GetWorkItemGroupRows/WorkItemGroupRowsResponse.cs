using Mustask.Application.Abstractions.Boards;

namespace Mustask.Application.Features.Boards.GetWorkItemGroupRows;

/// <summary>A page of a group of the grouped work-item list; no total and no page number (ADR-0036).</summary>
public sealed record WorkItemGroupRowsResponse(IReadOnlyList<AggregateItemView> Rows, string? NextCursor);
