using System.Text.RegularExpressions;

namespace Mustask.Infrastructure.Conventions;

internal static partial class StringExtensions
{
    /// <summary>
    /// Converts a PascalCase/camelCase identifier (as produced by EF Core's
    /// default relational conventions) to snake_case, e.g. "TaskId" -&gt;
    /// "task_id". Used to enforce the snake_case naming convention required
    /// by docs/conventions/backend.md ("База данных") on every table, column,
    /// key, and index without configuring each entity by hand.
    /// </summary>
    public static string ToSnakeCase(this string value) =>
        WordBoundaryRegex().Replace(value, "$1_$2").ToLowerInvariant();

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex WordBoundaryRegex();
}
