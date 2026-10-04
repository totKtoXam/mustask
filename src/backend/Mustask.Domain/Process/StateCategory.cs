namespace Mustask.Domain.Process;

/// <summary>
/// The category of a state (doc-007, doc-010 D-5): a closed set the logic
/// branches on. It is stored and served by its member name, never as a
/// number, and it is never shown: users read <see cref="StateCategoryLabels.Of"/>.
/// </summary>
public enum StateCategory
{
    Proposed,
    InProgress,
    Resolved,
    Completed,
    Removed,
}

/// <summary>
/// The Russian labels of the categories (doc-010 D-5). This is the one copy
/// of the words: the API response carries the label, and the client holds no
/// code-to-label map of its own.
/// </summary>
public static class StateCategoryLabels
{
    public static string Of(StateCategory category) => category switch
    {
        StateCategory.Proposed => "Новые",
        StateCategory.InProgress => "В работе",
        StateCategory.Resolved => "Решённые",
        StateCategory.Completed => "Завершённые",
        StateCategory.Removed => "Удалённые",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown state category."),
    };
}
