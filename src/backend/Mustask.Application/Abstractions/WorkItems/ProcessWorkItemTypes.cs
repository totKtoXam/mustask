using Mustask.Application.Abstractions.Process;
using Mustask.Domain.WorkItems;

namespace Mustask.Application.Abstractions.WorkItems;

/// <summary>
/// An organization's process as the work item aggregate reads it: each type as <see cref="WorkItemTypeStates"/>, built in
/// one place (it was built in two handlers) so that the item's own type, the hierarchy's removed ids
/// (<see cref="ProcessHierarchy.Of"/>) and Delete, Restore and Purge all read one list of "deleted" states — mod-005,
/// ADR-0030 rule 2.
/// </summary>
public static class ProcessWorkItemTypes
{
    /// <summary>The type of a process row as the aggregate needs it: its identity, key, kind and states.</summary>
    public static WorkItemTypeStates StatesOf(WorkItemTypeRow type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return new WorkItemTypeStates(
            type.Id,
            type.Key,
            type.Appearance,
            type.States.Select(state => new WorkItemTypeState(state.Id, state.Position, state.Category)).ToList());
    }

    /// <summary>The type of the process with that id, or <c>null</c> for an unknown or a foreign one.</summary>
    public static WorkItemTypeStates? Find(ProcessDefinitionRow process, Guid typeId)
    {
        ArgumentNullException.ThrowIfNull(process);

        var type = process.WorkItemTypes.SingleOrDefault(candidate => candidate.Id == typeId);

        return type is null ? null : StatesOf(type);
    }

    /// <summary>
    /// The item's own type in the organization's process. It is always there: an item's type never changes (BR-M3-1), so
    /// a type missing here is a defect, not an answer.
    /// </summary>
    public static WorkItemTypeStates OfItem(ProcessDefinitionRow process, Guid typeId) =>
        Find(process, typeId)
        ?? throw new InvalidOperationException($"The type {typeId} of a work item is not in its organization's process.");
}
