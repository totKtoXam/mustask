using Mustask.Domain.Process;

namespace Mustask.Domain.Boards;

/// <summary>
/// What a board card's checklist may hold (feat-032 BR-C-1): a direct, live child
/// of the card's item whose type is of one of three kinds — recognised by the
/// type's appearance token, never by its name. A child of another kind (a story
/// under a feature, a feature under an epic) is not in it.
/// </summary>
public static class CardChecklist
{
    /// <summary>The kinds a checklist holds, in the order the card's counters show them (BR-C-2).</summary>
    public static IReadOnlyList<AppearanceToken> Kinds { get; } =
        [AppearanceToken.Task, AppearanceToken.Bug, AppearanceToken.Testcase];

    /// <summary>Whether a child of a type of <paramref name="kind"/> is in its parent's checklist.</summary>
    public static bool Holds(AppearanceToken kind) => Kinds.Contains(kind);
}
