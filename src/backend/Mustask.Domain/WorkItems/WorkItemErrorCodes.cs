using Mustask.Shared.Errors;

namespace Mustask.Domain.WorkItems;

/// <summary>
/// Error codes of the work items module (M3, ADR-0005 registry; mod-003
/// api-contract § 4). The class lives in Domain because the module's own domain
/// throws these codes, and by Onion the domain cannot reference Application.
/// </summary>
/// <remarks>
/// Every constant is an "application code" (ADR-0005, item 5): it is marked with
/// <see cref="ErrorCodesAttribute"/> and must have an entry with Russian and
/// Kazakh texts in <c>contracts/errors/workitems.json</c> (checked by
/// <c>tests/Mustask.Contracts.Tests</c>).
/// </remarks>
[ErrorCodes]
public static class WorkItemErrorCodes
{
    /// <summary>
    /// «Рабочий элемент не найден.»: the identifier is malformed, its project key is not the addressed project's key,
    /// the project has no item with that number, or that item's type key differs (D-5).
    /// </summary>
    public const string WorkItemNotFound = "workitems.workitem.not_found";

    /// <summary>The top-level code of every <see cref="Mustask.Shared.Exceptions.ValidationException"/> of the module.</summary>
    public const string ValidationFailed = "workitems.validation_failed";

    /// <summary>Field reason of <c>title</c>: empty or white space only after trimming (BR-M3-3).</summary>
    public const string TitleRequired = "workitems.workitem.title_required";

    /// <summary>Field reason of <c>title</c>: more than 255 characters after trimming (BR-M3-3).</summary>
    public const string TitleTooLong = "workitems.workitem.title_too_long";

    /// <summary>Field reason of <c>title</c>: a line break in it (BR-M3-3).</summary>
    public const string TitleMultiline = "workitems.workitem.title_multiline";

    /// <summary>Field reason of <c>description</c>: more than 100 000 characters (BR-M3-19).</summary>
    public const string DescriptionTooLong = "workitems.workitem.description_too_long";

    /// <summary>Field reason of <c>requestId</c>: missing or not a well-formed id (BR-M3-26). A panel defect, not user input.</summary>
    public const string RequestIdInvalid = "workitems.workitem.request_id_invalid";

    /// <summary>The list's <c>page</c> is less than 1. A panel defect, not user input.</summary>
    public const string InvalidPage = "workitems.invalid_page";

    /// <summary>The type is malformed, unknown, or a type of another organization's process (BR-M3-1).</summary>
    public const string TypeNotFound = "workitems.workitem.type_not_found";

    /// <summary>The state is not one of the item's type's states, or it is that type's «Удалена» (BR-M3-21).</summary>
    public const string StateNotAllowed = "workitems.workitem.state_not_allowed";

    /// <summary>The newly set assignee is unknown to the directory or inactive (BR-M3-22).</summary>
    public const string AssigneeUnavailable = "workitems.workitem.assignee_unavailable";

    // --- mod-005: delete, restore, purge (its api-contract § 4) ----------------------------------------------------

    /// <summary>
    /// «Рабочий элемент удалён и доступен только для чтения.»: the item is in its type's «Удалена» (BR-DR-8). Answered by a
    /// save and by a second Delete, decided on the stored state under the item's lock.
    /// </summary>
    public const string WorkItemDeleted = "workitems.workitem.deleted";

    /// <summary>«Рабочий элемент не удалён.»: Restore or Purge of an item that is not in «Удалена» (BR-DR-10, BR-DR-12).</summary>
    public const string WorkItemNotDeleted = "workitems.workitem.not_deleted";

    // --- feat-001: the Parent/Child link (its api-contract § 5) ---------------------------------------------------

    /// <summary>
    /// «Родитель не найден в этом проекте.»: the named parent is malformed, names nothing, or is an item of another
    /// project or organization — one answer for all four (BR-H-1).
    /// </summary>
    public const string ParentNotFound = "workitems.link.parent_not_found";

    /// <summary>The new parent's level is below the item's own, by level rank (BR-H-2).</summary>
    public const string ParentLevelNotAllowed = "workitems.link.parent_level_not_allowed";

    /// <summary>The item is named as its own parent (BR-H-2).</summary>
    public const string SelfParent = "workitems.link.self_parent";

    /// <summary>The item is an ancestor of the new parent (BR-H-2); params <c>parent</c>, <c>item</c>, <c>path</c>.</summary>
    public const string Cycle = "workitems.link.cycle";

    /// <summary>The stored parent is not the one the user saw (BR-H-4).</summary>
    public const string ParentChanged = "workitems.link.parent_changed";

    /// <summary>The item, or the new parent, is in «Удалена» (BR-H-8).</summary>
    public const string ItemDeleted = "workitems.link.item_deleted";

    // --- feat-027: the hierarchy's depth bound (its D-9, api-contract § 5) ----------------------------------------

    /// <summary>
    /// The write would make a chain of parents longer than <see cref="Mustask.Domain.WorkItems.WorkItemHierarchy.MaxChainLength"/>
    /// items; param <c>limit</c>, a number. Answered by the parent write, the form's save with a <c>parent</c> and the
    /// create with a <c>parentId</c>, after the cycle check.
    /// </summary>
    public const string DepthExceeded = "workitems.link.depth_exceeded";

    // --- feat-022: the fields per type (its api-contract § 3.2, § 6), field reasons of ValidationFailed ----------

    /// <summary>A number field below 0 or above 9 999 (BR-M3-32).</summary>
    public const string NumberOutOfRange = "workitems.workitem.number_out_of_range";

    /// <summary>A decimal field with more than two digits after the point (BR-M3-32).</summary>
    public const string NumberTooPrecise = "workitems.workitem.number_too_precise";

    /// <summary>«Бизнес-ценность» with a fraction (BR-M3-32).</summary>
    public const string IntegerRequired = "workitems.workitem.integer_required";

    /// <summary>A fixed list's field with a key the list does not hold (BR-M3-31).</summary>
    public const string ValueNotInList = "workitems.workitem.value_not_in_list";

    /// <summary>A date before 2000-01-01 or after 2099-12-31 (BR-M3-35).</summary>
    public const string DateOutOfRange = "workitems.workitem.date_out_of_range";

    /// <summary>«Целевая дата» earlier than «Дата начала» once the save is applied, reported on the target (BR-M3-35).</summary>
    public const string TargetBeforeStart = "workitems.workitem.target_before_start";

    /// <summary>A type's text field over 100 000 characters (BR-M3-36).</summary>
    public const string TextTooLong = "workitems.workitem.text_too_long";

    /// <summary>A value sent for a field the item's type does not have (BR-M3-30).</summary>
    public const string FieldNotOnType = "workitems.workitem.field_not_on_type";
}
