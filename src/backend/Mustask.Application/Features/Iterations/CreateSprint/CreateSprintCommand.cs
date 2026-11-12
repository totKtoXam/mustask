namespace Mustask.Application.Features.Iterations.CreateSprint;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/teams/{teamId}/sprints</c> (US-1). Every value is the untrusted string it arrived as;
/// no name, or an empty one, takes the default «Спринт n».
/// </summary>
public sealed record CreateSprintCommand(string? OrganizationId, string? TeamId, string? Name, DateOnly? StartDate, DateOnly? EndDate);
