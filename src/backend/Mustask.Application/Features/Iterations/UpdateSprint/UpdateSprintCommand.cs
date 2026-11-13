namespace Mustask.Application.Features.Iterations.UpdateSprint;

/// <summary><c>PUT …/teams/{teamId}/sprints/{sprintId}</c> (US-1): the name and both dates, replacing all three.</summary>
public sealed record UpdateSprintCommand(
    string? OrganizationId, string? TeamId, string? SprintId, string? Name, DateOnly? StartDate, DateOnly? EndDate);
