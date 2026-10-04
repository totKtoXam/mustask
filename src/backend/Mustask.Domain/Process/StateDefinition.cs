namespace Mustask.Domain.Process;

/// <summary>
/// A state of one work item type. Two equal labels on two types are two
/// states with two identities (BR-M2-9).
/// </summary>
public sealed class StateDefinition
{
    public const int NameMaxLength = 64;
    public const int CategoryMaxLength = 16;

    /// <summary>The position of the initial state: the first one (BR-M2-2). Nothing is stored for it.</summary>
    public const int InitialPosition = 1;

    public Guid Id { get; private set; }

    public Guid WorkItemTypeId { get; private set; }

    public string Name { get; private set; } = null!;

    public StateCategory Category { get; private set; }

    /// <summary>1-based, dense and unique within the type.</summary>
    public int Position { get; private set; }

    private StateDefinition()
    {
        // EF materialization.
    }

    internal StateDefinition(Guid id, Guid workItemTypeId, string name, StateCategory category, int position)
    {
        Id = id;
        WorkItemTypeId = workItemTypeId;
        Name = name;
        Category = category;
        Position = position;
    }
}
