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
}
