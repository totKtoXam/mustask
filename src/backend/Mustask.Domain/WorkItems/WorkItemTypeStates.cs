using Mustask.Domain.Process;

namespace Mustask.Domain.WorkItems;

/// <summary>
/// One work item type of the organization's process as a work item needs it:
/// its identity, its key, its kind and its states (mod-001). States are
/// referenced by identity, never by name (mod-001 BR-M2-9): two equal labels on
/// two types are two states. The kind is the type's appearance token, fixed and
/// unique in its process: it decides the fields the type has (feat-022,
/// <see cref="WorkItemFieldSet"/>).
/// </summary>
public sealed record WorkItemTypeStates
{
    public WorkItemTypeStates(Guid typeId, string typeKey, AppearanceToken appearance, IReadOnlyList<WorkItemTypeState> states)
    {
        ArgumentException.ThrowIfNullOrEmpty(typeKey);
        ArgumentNullException.ThrowIfNull(states);

        if (!states.Any(state => state.Position == StateDefinition.InitialPosition))
        {
            throw new ArgumentException("A type has an initial state, the first in its order (BR-M2-2).", nameof(states));
        }

        TypeId = typeId;
        TypeKey = typeKey;
        Appearance = appearance;
        States = states;
    }

    public Guid TypeId { get; }

    public string TypeKey { get; }

    /// <summary>The type's kind (feat-022): what <see cref="WorkItemFieldSet"/> reads, never the name or the key.</summary>
    public AppearanceToken Appearance { get; }

    public IReadOnlyList<WorkItemTypeState> States { get; }

    /// <summary>The state a new item takes: the first in the seeded order, «Новая» on all five types (BR-M3-20).</summary>
    public Guid InitialStateId => States.Single(state => state.Position == StateDefinition.InitialPosition).Id;

    /// <summary>
    /// Whether an item of this type may be put in <paramref name="stateId"/>: any
    /// state of this type other than its «Удалена», whose category is
    /// <see cref="StateCategory.Removed"/> and which only mod-005's deletion
    /// reaches (BR-M3-21). A state of another type is never allowed.
    /// </summary>
    public bool Allows(Guid stateId) =>
        States.Any(state => state.Id == stateId && state.Category != StateCategory.Removed);
}

/// <summary>A state of a type: its identity, its 1-based position and its category.</summary>
public sealed record WorkItemTypeState(Guid Id, int Position, StateCategory Category);
