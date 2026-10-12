namespace Mustask.Domain.Process;

/// <summary>A backlog level of the blueprint (BR-M2-4).</summary>
public sealed record LevelBlueprint(string Name, int Rank, BacklogLevelKind Kind);

/// <summary>A state of a blueprint type. Its position is its place in the type's list, from 1.</summary>
public sealed record StateBlueprint(string Name, StateCategory Category);

/// <summary>A work item type of the blueprint. Its position is its place in the blueprint's list, from 1.</summary>
public sealed record TypeBlueprint(
    string Key,
    string Name,
    AppearanceToken Appearance,
    LevelBlueprint Level,
    IReadOnlyList<StateBlueprint> States);

/// <summary>
/// The content of one process as data. <see cref="EnsureValid"/> states the
/// invariants every process must satisfy, so an edit of the blueprint that
/// breaks one fails at once, at the first seed, and not in a reader's screen.
/// </summary>
public sealed record ProcessBlueprint(IReadOnlyList<LevelBlueprint> Levels, IReadOnlyList<TypeBlueprint> Types)
{
    /// <exception cref="InvalidOperationException">An invariant does not hold.</exception>
    public void EnsureValid()
    {
        RequireUnique(Levels.Select(level => level.Rank), "backlog level ranks");
        RequireUnique(Levels.Select(level => level.Name), "backlog level names");
        RequireUnique(Types.Select(type => type.Key), "type keys");
        RequireUnique(Types.Select(type => type.Name), "type names");
        RequireUnique(Types.Select(type => type.Appearance), "appearance tokens");

        foreach (var level in Levels)
        {
            RequireLength(level.Name, BacklogLevel.NameMaxLength, "backlog level name");
        }

        foreach (var type in Types)
        {
            RequireLength(type.Key, WorkItemType.KeyMaxLength, "type key");
            RequireLength(type.Name, WorkItemType.NameMaxLength, "type name");

            if (!Levels.Contains(type.Level))
            {
                throw new InvalidOperationException($"Type {type.Key} is on a level that is not a level of this process.");
            }

            if (type.States.Count == 0)
            {
                throw new InvalidOperationException($"Type {type.Key} has no states.");
            }

            RequireUnique(type.States.Select(state => state.Name), $"state names of {type.Key}");

            foreach (var state in type.States)
            {
                RequireLength(state.Name, StateDefinition.NameMaxLength, $"state name of {type.Key}");
            }

            var removed = type.States.Count(state => state.Category == StateCategory.Removed);

            if (removed != 1 || type.States[^1].Category != StateCategory.Removed)
            {
                throw new InvalidOperationException($"Type {type.Key} must have exactly one Removed state, and it must be the last.");
            }
        }

        // One name, one meaning (bug-004 D-3): across the types, not within one, where names are already unique.
        StateNameRule.Require(Types.SelectMany(type => type.States).Select(state => (state.Name, state.Category)));
    }

    private static void RequireUnique<T>(IEnumerable<T> values, string what)
    {
        var list = values.ToList();

        if (list.Distinct().Count() != list.Count)
        {
            throw new InvalidOperationException($"The {what} of a process must be unique.");
        }
    }

    private static void RequireLength(string value, int maxLength, string what)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
        {
            throw new InvalidOperationException($"The {what} must be 1 to {maxLength} characters.");
        }
    }
}

/// <summary>
/// The seeded process (doc-007 § "MVP: the seeded process", status.md D-6): four
/// backlog levels, five types and their states. This is the only place the
/// words and the order live. The unit tests compare the seed with the table in
/// plan-backend.md § 3 and api-contract.md § 1.4.
/// </summary>
public static class SeededProcess
{
    private static readonly LevelBlueprint EpicLevel = new("Эпики", 4, BacklogLevelKind.Portfolio);
    private static readonly LevelBlueprint FeatureLevel = new("Фичи", 3, BacklogLevelKind.Portfolio);
    private static readonly LevelBlueprint StoryLevel = new("Истории", 2, BacklogLevelKind.Requirement);
    private static readonly LevelBlueprint TaskLevel = new("Задачи", 1, BacklogLevelKind.Task);

    private static readonly StateBlueprint NewState = new("Новая", StateCategory.Proposed);
    private static readonly StateBlueprint ActiveState = new("Активна", StateCategory.InProgress);
    private static readonly StateBlueprint ClosedState = new("Закрыта", StateCategory.Completed);
    private static readonly StateBlueprint DeletedState = new("Удалена", StateCategory.Removed);

    // One name, one meaning (bug-004 D-3, which supersedes doc-010 D-4's two categories of this label):
    // "Разрешена" is Resolved — done, awaiting verification — on every type that has it, a bug and the
    // portfolio and story types alike. EnsureValid refuses a process in which a name is in two categories.
    private static readonly StateBlueprint ResolvedState = new("Разрешена", StateCategory.Resolved);

    private static readonly StateBlueprint[] FiveStates = [NewState, ActiveState, ResolvedState, ClosedState, DeletedState];

    /// <summary>The blueprint of every organization's process. Its types are in the backlog order of design DD-2: a story before a bug on their shared level.</summary>
    public static ProcessBlueprint Blueprint { get; } = new(
        [EpicLevel, FeatureLevel, StoryLevel, TaskLevel],
        [
            new("EPIC", "Эпик", AppearanceToken.Epic, EpicLevel, FiveStates),
            new("FEAT", "Фича", AppearanceToken.Feature, FeatureLevel, FiveStates),
            new("US", "Пользовательская история", AppearanceToken.Story, StoryLevel, FiveStates),
            new("BUG", "Баг", AppearanceToken.Bug, StoryLevel, FiveStates),
            new("TASK", "Задача", AppearanceToken.Task, TaskLevel, [NewState, ActiveState, ClosedState, DeletedState]),
        ]);
}
