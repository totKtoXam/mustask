using Mustask.Application.Abstractions.Boards;

namespace Mustask.Application.Features.WorkItems.GetOrganizationWorkItems;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/workitems?page=</c> (feat-048, BR-NV-12): the organization-wide list. The selectors
/// and filters are <see cref="AggregateQuery"/>'s; <paramref name="Page"/> is 1-based, <c>null</c> for the first.
/// </summary>
public sealed record GetOrganizationWorkItemsQuery(AggregateQuery Request, int? Page);
