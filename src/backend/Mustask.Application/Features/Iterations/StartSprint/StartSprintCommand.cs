namespace Mustask.Application.Features.Iterations.StartSprint;

/// <summary><c>POST …/teams/{teamId}/sprints/{sprintId}/start</c> (US-2).</summary>
public sealed record StartSprintCommand(string? OrganizationId, string? TeamId, string? SprintId);
