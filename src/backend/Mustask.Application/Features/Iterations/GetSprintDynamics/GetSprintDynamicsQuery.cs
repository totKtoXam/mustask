namespace Mustask.Application.Features.Iterations.GetSprintDynamics;

/// <summary>
/// <c>GET …/teams/{teamId}/sprints/{sprintId}/dynamics?level=&amp;from=&amp;to=</c> (US-9). Every value is the untrusted string it
/// arrived as; an unusable range is the default one.
/// </summary>
public sealed record GetSprintDynamicsQuery(
    string? OrganizationId, string? TeamId, string? SprintId, string? Level, string? From, string? To);
