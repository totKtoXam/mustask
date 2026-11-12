namespace Mustask.Application.Features.Iterations.GetSprintParticipants;

/// <summary>
/// <c>GET …/teams/{teamId}/sprints/{sprintId}/participants?level=&amp;from=&amp;to=&amp;offset=</c> (US-10). Every value is the
/// untrusted string it arrived as.
/// </summary>
public sealed record GetSprintParticipantsQuery(
    string? OrganizationId, string? TeamId, string? SprintId, string? Level, string? From, string? To, string? Offset);
