using Mustask.Application.Abstractions.Boards;
using Mustask.Domain.Boards;

namespace Mustask.Application.Features.Boards.GetViewGroups;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/{view}/{level}/groups?grouping=</c> (feat-048 api-contract section 2.1): the grouped
/// count read of the headers, the one read that opens an aggregate view. <paramref name="View"/> is the route's literal view;
/// <paramref name="Request"/> carries the level (none for the work-item list), the grouping, the selectors and the filters.
/// </summary>
public sealed record GetViewGroupsQuery(AggregateView View, AggregateQuery Request);
