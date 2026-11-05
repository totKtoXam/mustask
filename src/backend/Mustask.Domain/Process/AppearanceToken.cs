namespace Mustask.Domain.Process;

/// <summary>
/// The appearance token of a work item type (status.md D-6): one of six names
/// the client maps to an icon and a colour through its theme. The process
/// stores no icon and no colour of its own. The wire spelling is lowercase
/// (<see cref="AppearanceTokenNames"/>), both in the database and in the API.
/// </summary>
/// <remarks>
/// The member <c>Task</c> must always be written qualified
/// (<c>AppearanceToken.Task</c>): the implicit usings bring in
/// <see cref="System.Threading.Tasks.Task"/>.
/// </remarks>
public enum AppearanceToken
{
    Epic,
    Feature,
    Story,
    Task,
    Bug,

    /// <summary>feat-032 D-3: a test case, a child of a story; feat-031 adds the type of this appearance.</summary>
    Testcase,
}

/// <summary>The one name map of <see cref="AppearanceToken"/>, shared by the EF converter and the response.</summary>
public static class AppearanceTokenNames
{
    public static string ToWireName(this AppearanceToken token) => token switch
    {
        AppearanceToken.Epic => "epic",
        AppearanceToken.Feature => "feature",
        AppearanceToken.Story => "story",
        AppearanceToken.Task => "task",
        AppearanceToken.Bug => "bug",
        AppearanceToken.Testcase => "testcase",
        _ => throw new ArgumentOutOfRangeException(nameof(token), token, "Unknown appearance token."),
    };

    public static AppearanceToken FromWireName(string name) => name switch
    {
        "epic" => AppearanceToken.Epic,
        "feature" => AppearanceToken.Feature,
        "story" => AppearanceToken.Story,
        "task" => AppearanceToken.Task,
        "bug" => AppearanceToken.Bug,
        "testcase" => AppearanceToken.Testcase,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown appearance token name."),
    };
}
