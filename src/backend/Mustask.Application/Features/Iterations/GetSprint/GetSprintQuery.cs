namespace Mustask.Application.Features.Iterations.GetSprint;

/// <summary><c>GET …/teams/{teamId}/sprints/{sprintId}</c> (US-5): the header of a Closed sprint's page.</summary>
public sealed record GetSprintQuery(string? OrganizationId, string? TeamId, string? SprintId);
