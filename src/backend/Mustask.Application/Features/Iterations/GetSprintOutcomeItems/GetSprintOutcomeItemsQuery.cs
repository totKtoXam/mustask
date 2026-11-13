namespace Mustask.Application.Features.Iterations.GetSprintOutcomeItems;

/// <summary><c>GET …/teams/{teamId}/sprints/{sprintId}/items?offset=</c> (US-5): a Closed sprint's done items.</summary>
public sealed record GetSprintOutcomeItemsQuery(string? OrganizationId, string? TeamId, string? SprintId, string? Offset);
