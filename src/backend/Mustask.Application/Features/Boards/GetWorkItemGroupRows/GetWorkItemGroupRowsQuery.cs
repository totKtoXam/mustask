using Mustask.Application.Abstractions.Boards;

namespace Mustask.Application.Features.Boards.GetWorkItemGroupRows;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/workitems/rows?grouping=&amp;group=&amp;limit=&amp;cursor=</c> (feat-048
/// api-contract section 2.4): one page of one group of the grouped work-item list.
/// </summary>
public sealed record GetWorkItemGroupRowsQuery(AggregateQuery Request);
