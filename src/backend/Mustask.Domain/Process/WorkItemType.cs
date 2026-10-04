namespace Mustask.Domain.Process;

/// <summary>
/// A work item type of a process: a key that is Latin, fixed and unique in the
/// process, a Russian name, one appearance token, its level and its ordered
/// states. The level is a foreign key without a navigation, so adding a type
/// never re-adds its level, and the type has no navigation back to its process.
/// The database also refuses a level of another process (qa.md D-2).
/// </summary>
public sealed class WorkItemType
{
    public const int KeyMaxLength = 10;
    public const int NameMaxLength = 64;
    public const int AppearanceMaxLength = 16;

    private readonly List<StateDefinition> _states = [];

    public Guid Id { get; private set; }

    public ProcessId ProcessId { get; private set; }

    public Guid BacklogLevelId { get; private set; }

    /// <summary>The identifier within the process (EPIC, FEAT, US, BUG, TASK). Not named <c>Code</c>: it is unique per process, not globally.</summary>
    public string Key { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public AppearanceToken Appearance { get; private set; }

    /// <summary>The seed's order of types; unique in the process.</summary>
    public int Position { get; private set; }

    public IReadOnlyList<StateDefinition> States => _states;

    private WorkItemType()
    {
        // EF materialization.
    }

    internal WorkItemType(
        Guid id, ProcessId processId, Guid backlogLevelId, string key, string name, AppearanceToken appearance, int position)
    {
        Id = id;
        ProcessId = processId;
        BacklogLevelId = backlogLevelId;
        Key = key;
        Name = name;
        Appearance = appearance;
        Position = position;
    }

    internal void AddState(string name, StateCategory category, int position) =>
        _states.Add(new StateDefinition(Guid.CreateVersion7(), Id, name, category, position));
}
