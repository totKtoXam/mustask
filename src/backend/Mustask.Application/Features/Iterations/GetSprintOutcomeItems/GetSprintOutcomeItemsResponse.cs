namespace Mustask.Application.Features.Iterations.GetSprintOutcomeItems;

/// <summary>A page of a Closed sprint's done items; the totals are of the whole list, not of the page.</summary>
public sealed record GetSprintOutcomeItemsResponse(IReadOnlyList<OutcomeItemResponse> Items, int TotalItems, decimal TotalStoryPoints);

/// <summary>One done item; a purged one has no title.</summary>
public sealed record OutcomeItemResponse(string Identifier, string? Title, OutcomeProjectResponse Project, decimal StoryPoints, bool Purged);

public sealed record OutcomeProjectResponse(Guid Id, string Name);
