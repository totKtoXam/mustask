namespace Mustask.Application.Features.Iterations.GetCloseSprintPreview;

/// <summary><c>GET …/teams/{teamId}/sprints/{sprintId}/close-preview</c> (US-5).</summary>
public sealed record GetCloseSprintPreviewQuery(string? OrganizationId, string? TeamId, string? SprintId);
