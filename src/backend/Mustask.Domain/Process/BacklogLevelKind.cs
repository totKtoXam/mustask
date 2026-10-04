namespace Mustask.Domain.Process;

/// <summary>
/// The kind of a backlog level (mod-015 BR-4, doc-007): portfolio, requirement
/// or task. The wire spelling is lowercase (<see cref="BacklogLevelKindNames"/>),
/// both in the database and in the API.
/// </summary>
/// <remarks>
/// The member <c>Task</c> must always be written qualified
/// (<c>BacklogLevelKind.Task</c>): the implicit usings bring in
/// <see cref="System.Threading.Tasks.Task"/>.
/// </remarks>
public enum BacklogLevelKind
{
    Portfolio,
    Requirement,
    Task,
}

/// <summary>The one name map of <see cref="BacklogLevelKind"/>, shared by the EF converter and the response.</summary>
public static class BacklogLevelKindNames
{
    public static string ToWireName(this BacklogLevelKind kind) => kind switch
    {
        BacklogLevelKind.Portfolio => "portfolio",
        BacklogLevelKind.Requirement => "requirement",
        BacklogLevelKind.Task => "task",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown backlog level kind."),
    };

    public static BacklogLevelKind FromWireName(string name) => name switch
    {
        "portfolio" => BacklogLevelKind.Portfolio,
        "requirement" => BacklogLevelKind.Requirement,
        "task" => BacklogLevelKind.Task,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown backlog level kind name."),
    };
}
