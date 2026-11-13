namespace Mustask.Infrastructure.Iterations;

/// <summary>
/// The team's number sequence (BR-7) as a row: the last number the team issued. Storage only, written by raw SQL under a row lock
/// (<see cref="SprintRepository"/>), as the work items' per-project counter is; a team gets its row with its first sprint.
/// </summary>
internal sealed class SprintCounter
{
    public const string Table = "sprint_counters";

    private SprintCounter()
    {
        // EF materialization.
    }

    public Guid TeamId { get; private set; }

    public int LastNumber { get; private set; }
}
