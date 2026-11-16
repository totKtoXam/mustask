using Mustask.Domain.Iterations;

namespace Mustask.Application.Abstractions.Iterations;

/// <summary>
/// The read side of sprints (mod-007 api-contract § 2): projections straight from the store, <c>AsNoTracking</c>, a fixed number of
/// statements whatever the sprints, items, projects or assignees. Every figure of a Closed sprint comes from its close snapshot;
/// an item of a deactivated project is counted nowhere until the project is restored (BR-9).
/// </summary>
public interface ISprintReadModel
{
    /// <summary>The sprint of the team with its creator and closer marks, or <c>null</c>.</summary>
    Task<SprintView?> FindAsync(Guid teamId, Guid sprintId, CancellationToken cancellationToken);

    /// <summary>One page of the team's sprints, highest number first, with their counts (3 statements).</summary>
    Task<SprintListPage> ListAsync(Guid teamId, int? before, bool openOnly, Guid storiesLevelId, CancellationToken cancellationToken);

    /// <summary>The Active sprint's items split into what will move and what stays, and the team's Planned sprints (3 statements).</summary>
    Task<ClosePreview> ClosePreviewAsync(Guid teamId, Guid sprintId, CancellationToken cancellationToken);

    /// <summary>A Closed sprint's done items, all levels: the header's count and points (1 statement).</summary>
    Task<(int Items, decimal StoryPoints)> DoneTotalsAsync(Guid sprintId, CancellationToken cancellationToken);

    /// <summary>
    /// The team's other Closed sprints whose end date lies in the range, the latest first, at most
    /// <see cref="SprintLimits.MaxRangeSprints"/>, with how many the range holds (2 statements).
    /// </summary>
    Task<RangeSprints> FindRangeAsync(Guid teamId, Guid sprintId, SprintRange range, CancellationToken cancellationToken);

    /// <summary>The figures per sprint of <paramref name="sprintIds"/> at the level, in one statement; a sprint holding nothing is absent.</summary>
    Task<IReadOnlyDictionary<Guid, SprintCounts>> CountsBySprintAsync(IReadOnlyCollection<Guid> sprintIds, Guid levelId, CancellationToken cancellationToken);

    /// <summary>The figures per (sprint, assignee) at the level in one statement; <c>AssigneeId</c> is <c>null</c> for no assignee.</summary>
    Task<IReadOnlyList<ParticipantCounts>> CountsByParticipantAsync(IReadOnlyCollection<Guid> sprintIds, Guid levelId, CancellationToken cancellationToken);

    
    Task<IReadOnlySet<string>> OrganizationMemberIdsAsync(Guid organizationId, IReadOnlyCollection<string> userIds, CancellationToken cancellationToken);

    /// <summary>One page of a Closed sprint's done items and the totals (2 statements).</summary>
    Task<OutcomeItemsPage> OutcomeItemsAsync(Guid sprintId, int offset, CancellationToken cancellationToken);
}

public sealed record SprintView(
    Guid Id,
    Guid TeamId,
    int Number,
    string Name,
    string State,
    DateOnly? StartDate,
    DateOnly? EndDate,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? ClosedAt,
    string? ClosedBy);

public sealed record SprintRowView(
    Guid Id,
    int Number,
    string Name,
    string State,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int ItemCount,
    decimal StoryPoints,
    int? ClosedItems,
    decimal? ClosedStoryPoints);

public sealed record SprintListPage(IReadOnlyList<SprintRowView> Items, int? NextBefore);

public sealed record CountView(int Items, decimal StoryPoints);

public sealed record DestinationView(Guid Id, string Name, DateOnly? StartDate, DateOnly? EndDate);

public sealed record ClosePreview(CountView Moving, CountView Staying, IReadOnlyList<DestinationView> Destinations);

public sealed record RangeSprint(Guid Id, int Number, string Name, DateOnly EndDate);

public sealed record RangeSprints(int Total, IReadOnlyList<RangeSprint> Sprints);

public sealed record ParticipantCounts(Guid SprintId, string? AssigneeId, SprintCounts Counts);

public sealed record OutcomeItemView(
    string Identifier, string? Title, Guid ProjectId, string ProjectName, decimal StoryPoints, bool Purged);

public sealed record OutcomeItemsPage(IReadOnlyList<OutcomeItemView> Items, int TotalItems, decimal TotalStoryPoints);
