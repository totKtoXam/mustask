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

    public const string NumberPositive = "ck_work_items_number_positive";

    public static readonly string TitleLengthCheck = $"char_length(title) BETWEEN 1 AND {WorkItemLimits.TitleMaxLength}";

    public static readonly string DescriptionLengthCheck = $"char_length(description) <= {WorkItemLimits.DescriptionMaxLength}";
}
