namespace Mustask.Infrastructure.Iterations;

/// <summary>The names of the iterations module's indexes, keys and checks, in one place (mod-007 plan-backend, "Database").</summary>
internal static class IterationsConstraints
{
    /// <summary>BR-7: a number is issued once in its team.</summary>
    public const string TeamNumber = "ix_sprints_team_id_number";

    /// <summary>BR-7: a name is unique among the team's sprints (a removed sprint is gone, so it frees its name).</summary>
    public const string TeamName = "ix_sprints_team_id_normalized_name";

    /// <summary>BR-3: at most one Active sprint per team, the database's backstop to the start's check.</summary>
    public const string TeamActive = "ix_sprints_team_id_active";

    /// <summary>BR-15: the team's Closed sprints by end date, the range's read.</summary>
    public const string TeamClosedEnd = "ix_sprints_team_id_end_date_closed";

    /// <summary>BR-2: an item's sprint is a sprint of the item's own owning team.</summary>
    public const string ItemSprintKey = "fk_work_items_sprints_sprint_id_owning_team_id";

    /// <summary>The close's and the removal's read of a sprint's items.</summary>
    public const string ItemSprint = "ix_work_items_sprint_id";

    /// <summary>BR-2: an item with no owning team has no sprint.</summary>
    public const string ItemSprintNeedsTeam = "ck_work_items_sprint_needs_team";

    /// <summary>BR-14: the snapshot's reads by sprint and level.</summary>
    public const string SnapshotSprintLevel = "ix_sprint_snapshot_items_sprint_id_level_id";
}
