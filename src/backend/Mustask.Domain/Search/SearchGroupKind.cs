namespace Mustask.Domain.Search;

/// <summary>
/// The groups of the quick search (feat-071 api-contract). The declaration order is the fixed tie order of BR-GS-8 («Проекты»,
/// «Команды», «Департаменты», «Области», «Пользователи»; the work items group of the later line goes before them).
/// </summary>
public enum SearchGroupKind
{
    Projects = 0,
    Teams = 1,
    Departments = 2,
    Areas = 3,
    Users = 4,
}

/// <summary>The wire spelling of <see cref="SearchGroupKind"/>.</summary>
public static class SearchGroupKinds
{
    public static string ToWireName(this SearchGroupKind kind) => kind switch
    {
        SearchGroupKind.Projects => "projects",
        SearchGroupKind.Teams => "teams",
        SearchGroupKind.Departments => "departments",
        SearchGroupKind.Areas => "areas",
        SearchGroupKind.Users => "users",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>The kind named by <paramref name="name"/> (exactly the wire spelling), or <c>false</c>.</summary>
    public static bool TryParse(string? name, out SearchGroupKind kind)
    {
        foreach (var candidate in Enum.GetValues<SearchGroupKind>())
        {
            if (candidate.ToWireName() == name)
            {
                kind = candidate;

                return true;
            }
        }

        kind = default;

        return false;
    }
}
