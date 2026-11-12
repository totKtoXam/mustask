namespace Mustask.Application.Features.Iterations.CloseSprint;

/// <summary>
/// <c>POST …/teams/{teamId}/sprints/{sprintId}/close</c> (US-5). <paramref name="DestinationSprintId"/> is the untrusted string it
/// arrived as; none (absent or <c>null</c>) means «Без спринта».
/// </summary>
public sealed record CloseSprintCommand(string? OrganizationId, string? TeamId, string? SprintId, string? DestinationSprintId);
