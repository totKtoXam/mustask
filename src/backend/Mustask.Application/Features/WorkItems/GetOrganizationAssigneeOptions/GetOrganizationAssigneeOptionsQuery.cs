using Mustask.Application.Abstractions.Boards;

namespace Mustask.Application.Features.WorkItems.GetOrganizationAssigneeOptions;

/// <summary><c>GET /api/organizations/{organizationId}/workitems/assignee-options</c> (feat-048): the options of the organization-wide list's assignee filter.</summary>
public sealed record GetOrganizationAssigneeOptionsQuery(AggregateQuery Request);
