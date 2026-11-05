namespace Mustask.Infrastructure.Boards;

/// <summary>
/// One project backlog (project, level) that has been reordered at least once: the row a project reorder locks
/// <c>FOR UPDATE</c>, so the reorders of one project backlog run one after another (feat-039 BR-PB-4, ADR-0011), and
/// the parent of the backlog's places. The team's <see cref="BacklogOrder"/> is the same thing for a team's backlog
/// and the two are never joined (ADR-0028 rule 9). Storage only — written by <see cref="ProjectBacklogRepository"/> with
/// raw SQL — so it lives here and not in the domain; the order itself is the domain's
/// <see cref="Mustask.Domain.Boards.StackRank"/>. A project backlog gets its row with its first reorder.
/// </summary>
internal sealed class ProjectBacklogOrder
{
    public const string Table = "project_backlog_orders";

    private ProjectBacklogOrder()
    {
        // EF materialization.
    }

    public Guid ProjectId { get; private set; }

    /// <summary>The process level the backlog is of (mod-001's <c>backlog_levels</c>).</summary>
    public Guid BacklogLevelId { get; private set; }
}
