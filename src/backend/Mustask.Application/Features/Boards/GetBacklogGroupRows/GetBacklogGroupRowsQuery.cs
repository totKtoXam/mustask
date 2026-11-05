using Mustask.Application.Abstractions.Boards;

namespace Mustask.Application.Features.Boards.GetBacklogGroupRows;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/backlogs/{level}/rows?grouping=&amp;group=&amp;limit=&amp;cursor=</c> (feat-048
/// api-contract section 2.2): one page of one group of an aggregate backlog.
/// </summary>
public sealed record GetBacklogGroupRowsQuery(AggregateQuery Request);
