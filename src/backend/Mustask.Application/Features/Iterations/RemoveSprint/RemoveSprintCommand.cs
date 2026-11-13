namespace Mustask.Application.Features.Iterations.RemoveSprint;

/// <summary><c>DELETE …/teams/{teamId}/sprints/{sprintId}</c> (US-2).</summary>
public sealed record RemoveSprintCommand(string? OrganizationId, string? TeamId, string? SprintId);
