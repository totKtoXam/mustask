using System.Globalization;
using Mustask.Domain.WorkItems;

namespace Mustask.Infrastructure.WorkItems;

/// <summary>The names of the work items module's indexes and checks, in one place (mod-003 status.md § Plan).</summary>
internal static class WorkItemsConstraints
{
    /// <summary>BR-M3-5: a number is issued once in its project.</summary>
    public const string ProjectNumber = "ix_work_items_project_id_number";

    /// <summary>BR-M3-26: one item per submission of the create dialog, per project.</summary>
    public const string ProjectCreationRequest = "ix_work_items_project_id_creation_request_id";

    /// <summary>BR-M3-29: the list's order, newest change first, then number descending.</summary>
    public const string ProjectChangedAt = "ix_work_items_project_id_changed_at_number";

    /// <summary>BR-M3-24: the area deletion looks for any item of the project in the area.</summary>
    public const string ProjectArea = "ix_work_items_project_id_area_id";

    /// <summary>mod-006 BR-M4-5: a team's backlog of a level reads the project's items of the level's types in the team's areas.</summary>
    public const string ProjectTypeArea = "ix_work_items_project_id_type_id_area_id";

    /// <summary>feat-001 BR-H-1, BR-H-7: the parent's composite key, and a parent's children.</summary>
    public const string ProjectParent = "ix_work_items_project_id_parent_id";

    /// <summary>mod-017 BR-OT-5: a team's views read the project's items of a team and a level's types.</summary>
    public const string ProjectOwningTeamType = "ix_work_items_project_id_owning_team_id_type_id";

    /// <summary>mod-017 ADR-0028 rule 5: the target of the team place's key onto the item's current owning team.</summary>
    public const string IdOwningTeam = "ix_work_items_id_owning_team_id";

    /// <summary>mod-017 BR-OT-13: the owner key of an item, whose violation is a team that is no longer there.</summary>
    public const string OwningTeamKey = "fk_work_items_teams_project_id_owning_team_id";

    public const string NumberPositive = "ck_work_items_number_positive";

    /// <summary>feat-001 BR-H-2: an item is never its own parent.</summary>
    public const string ParentNotSelf = "ck_work_items_parent_not_self";

    public static readonly string TitleLengthCheck = $"char_length(title) BETWEEN 1 AND {WorkItemLimits.TitleMaxLength}";

    public static readonly string DescriptionLengthCheck = $"char_length(description) <= {WorkItemLimits.DescriptionMaxLength}";

    /// <summary>feat-022: a number field's digits in all, two of them after the point — 9 999.99 at most.</summary>
    public const int NumberPrecision = 6;

    /// <summary>feat-022: the longest key of «Вид работ» and «Вид ценности» («Documentation», 13), with room.</summary>
    public const int ListKeyMaxLength = 16;

    /// <summary>
    /// feat-022 api-contract § 1: every field's range, by name. Which fields a type has is the domain's alone
    /// (<see cref="WorkItemFieldSet"/>): a check cannot see the type's kind, which lives in another table.
    /// </summary>
    public static readonly IReadOnlyList<(string Name, string Sql)> FieldChecks =
    [
        ("ck_work_items_priority", $"priority BETWEEN {WorkItemFieldRules.HighestPriority} AND {WorkItemFieldRules.LowestPriority}"),
        ("ck_work_items_risk", $"risk BETWEEN {WorkItemFieldRules.HighestRisk} AND {WorkItemFieldRules.LowestRisk}"),
        ("ck_work_items_severity", $"severity BETWEEN {WorkItemFieldRules.HighestSeverity} AND {WorkItemFieldRules.LowestSeverity}"),
        ("ck_work_items_story_points", NumberCheck("story_points")),
        ("ck_work_items_effort", NumberCheck("effort")),
        ("ck_work_items_business_value", NumberCheck("business_value")),
        ("ck_work_items_time_criticality", NumberCheck("time_criticality")),
        ("ck_work_items_original_estimate", NumberCheck("original_estimate")),
        ("ck_work_items_remaining_work", NumberCheck("remaining_work")),
        ("ck_work_items_completed_work", NumberCheck("completed_work")),
        ("ck_work_items_start_date", DateCheck("start_date")),
        ("ck_work_items_target_date", DateCheck("target_date")),
        ("ck_work_items_target_not_before_start", "target_date >= start_date"),
        ("ck_work_items_activity", KeyCheck<WorkItemActivity>("activity")),
        ("ck_work_items_value_area", KeyCheck<WorkItemValueArea>("value_area")),
        ("ck_work_items_acceptance_criteria_length", TextCheck("acceptance_criteria")),
        ("ck_work_items_repro_steps_length", TextCheck("repro_steps")),
        ("ck_work_items_system_info_length", TextCheck("system_info")),
    ];

    // A check passes on NULL, so every optional field may be empty.
    private static string NumberCheck(string column) => $"{column} BETWEEN 0 AND {WorkItemFieldRules.NumberMax}";

    private static string DateCheck(string column) =>
        $"{column} BETWEEN DATE '{IsoDate(WorkItemFieldRules.EarliestDate)}' AND DATE '{IsoDate(WorkItemFieldRules.LatestDate)}'";

    private static string IsoDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string KeyCheck<T>(string column)
        where T : struct, Enum =>
        $"{column} IN ({string.Join(", ", Enum.GetNames<T>().Select(name => $"'{name}'"))})";

    private static string TextCheck(string column) => $"char_length({column}) <= {WorkItemFieldRules.TextMaxLength}";
}
