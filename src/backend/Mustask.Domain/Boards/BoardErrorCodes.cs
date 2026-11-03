using Mustask.Shared.Errors;

namespace Mustask.Domain.Boards;

/// <summary>
/// Error codes of the boards and backlogs module (M4, ADR-0005 registry;
/// mod-006 api-contract § 4). The class lives in Domain because the module's
/// own domain throws <see cref="ReorderStale"/>.
/// </summary>
/// <remarks>
/// Every constant is an "application code" (ADR-0005, item 5): it is marked with
/// <see cref="ErrorCodesAttribute"/> and must have an entry with Russian and
/// Kazakh texts in <c>contracts/errors/boards.json</c> (checked by
/// <c>tests/Mustask.Contracts.Tests</c>).
/// </remarks>
[ErrorCodes]
public static class BoardErrorCodes
{
    /// <summary>
    /// «Не удалось переместить: бэклог изменился. …»: the moved item, or the item it was to go after, is not on that
    /// backlog any more (BR-M4-26). Nothing changes.
    /// </summary>
    public const string ReorderStale = "boards.backlog.reorder_stale";

    /// <summary>The <c>{level}</c> segment is not <c>epics</c>, <c>features</c> or <c>stories</c>, or the process has no such level.</summary>
    public const string LevelNotFound = "boards.level.not_found";

    /// <summary>The top-level code of every <see cref="Mustask.Shared.Exceptions.ValidationException"/> of the module.</summary>
    public const string ValidationFailed = "boards.validation_failed";

    /// <summary>Field reason of <c>position</c>: missing, or not <c>top</c>, <c>bottom</c> or <c>after</c>. A panel defect.</summary>
    public const string PositionInvalid = "boards.backlog.position_invalid";

    /// <summary>Field reason of <c>cursor</c> (feat-048, ADR-0036): malformed, or issued for another view, level, grouping, group or filter.</summary>
    public const string InvalidCursor = "boards.invalid_cursor";

    /// <summary>Field reason of <c>grouping</c> (feat-048 BR-AV-2): not <c>none</c>, <c>department</c>, <c>project</c> or <c>team</c>, or one the level does not offer.</summary>
    public const string InvalidGrouping = "boards.invalid_grouping";

    /// <summary>Field reason of <c>group</c>, <c>limit</c> or <c>column</c> on a group or cell read (feat-048): missing where needed, malformed or out of range.</summary>
    public const string InvalidParameter = "boards.invalid_parameter";
}
