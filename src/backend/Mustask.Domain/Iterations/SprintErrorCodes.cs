using Mustask.Shared.Errors;

namespace Mustask.Domain.Iterations;

/// <summary>
/// Error codes of the iterations module (mod-007 api-contract § 4, ADR-0005 registry). The texts are in
/// <c>contracts/errors/iterations.json</c>; every constant is an application code and carries the attribute.
/// </summary>
[ErrorCodes]
public static class SprintErrorCodes
{
    /// <summary>The top-level code of every <see cref="Mustask.Shared.Exceptions.ValidationException"/> of the module.</summary>
    public const string ValidationFailed = "iterations.validation_failed";

    /// <summary>Another sprint of the team holds the name (ignoring case and surrounding spaces).</summary>
    public const string NameTaken = "iterations.sprint.name_taken";

    /// <summary>Field reason of <c>name</c>: empty, spaces only, or over 100 characters after trimming.</summary>
    public const string NameInvalid = "iterations.sprint.name_invalid";

    /// <summary>Field reason of <c>endDate</c>: one date sent without the other.</summary>
    public const string DatesUnpaired = "iterations.sprint.dates_unpaired";

    /// <summary>Field reason of <c>endDate</c>: the finish is before the start.</summary>
    public const string DatesReversed = "iterations.sprint.dates_reversed";

    /// <summary>An Active sprint's dates are changed but not cleared.</summary>
    public const string ActiveDatesRequired = "iterations.sprint.active_dates_required";

    /// <summary>A Closed sprint's dates never change.</summary>
    public const string ClosedDatesLocked = "iterations.sprint.closed_dates_locked";

    /// <summary>Starting needs both dates.</summary>
    public const string StartDatesRequired = "iterations.sprint.start_dates_required";

    /// <summary>The team already has an Active sprint.</summary>
    public const string ActiveExists = "iterations.sprint.active_exists";

    /// <summary>Only a Planned sprint is started.</summary>
    public const string NotPlanned = "iterations.sprint.not_planned";

    /// <summary>Only an Active sprint is closed.</summary>
    public const string NotActive = "iterations.sprint.not_active";

    /// <summary>Only a Planned sprint holding no item outside «Удалена» is removed.</summary>
    public const string NotRemovable = "iterations.sprint.not_removable";

    /// <summary>The sprint is malformed, unknown, of another organization or team, removed, or (for the outcome page) not Closed.</summary>
    public const string NotFound = "iterations.sprint.not_found";

    /// <summary>The close's destination exists but is not a Planned sprint of this team.</summary>
    public const string CloseDestinationInvalid = "iterations.sprint.close_destination_invalid";

    /// <summary>An item's sprint is a sprint of another team than its owning team.</summary>
    public const string OtherTeam = "iterations.sprint.other_team";

    /// <summary>An item with no owning team has no sprint.</summary>
    public const string TeamRequired = "iterations.sprint.team_required";

    /// <summary>An item does not join a Closed sprint.</summary>
    public const string ClosedTarget = "iterations.sprint.closed_target";
}
