using Mustask.Application.Abstractions.Boards;

namespace Mustask.Application.Features.Boards.GetBoardCells;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/boards/{level}/cells?grouping=&amp;group=&amp;column=&amp;limit=&amp;cursor=</c>
/// (feat-048 api-contract section 2.3): one page of the cards of one cell, a lane (group) by a column.
/// </summary>
public sealed record GetBoardCellsQuery(AggregateQuery Request);
