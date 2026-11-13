namespace Mustask.Application.Features.Iterations.GetSprint;

/// <summary>A Closed sprint's header: the sprint, who closed it, and what it finished (the done part of the snapshot, BR-6).</summary>
public sealed record GetSprintResponse(SprintResponse Sprint, string? ClosedByName, int ItemCount, decimal StoryPoints);
