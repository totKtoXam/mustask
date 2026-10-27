using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;

namespace Mustask.Domain.Tests.WorkItems;

/// <summary>
/// mod-005 (ADR-0030 rule 1 and 2): the type's Removed state and the one in-memory definition of "deleted" that
/// <see cref="WorkItemTypeStates.Allows"/> and the hierarchy's <see cref="HierarchyTypes.IsDeleted"/> both read.
/// </summary>
public sealed class WorkItemTypeStatesTests
{
    private static WorkItemTypeStates Of(params StateCategory[] categories) =>
        new(
            Guid.CreateVersion7(),
            "US",
            AppearanceToken.Story,
            categories.Select((category, index) => new WorkItemTypeState(Guid.CreateVersion7(), index + 1, category)).ToList());

    [Fact]
    public void RemovedStateId_IsTheOneRemovedState_AndThrowsWithNoneOrTwo()
    {
        var one = Of(StateCategory.Proposed, StateCategory.Completed, StateCategory.Removed);

        Assert.Equal(one.States.Single(state => state.Category == StateCategory.Removed).Id, one.RemovedStateId);
        Assert.Single(one.RemovedStateIds);

        var none = Of(StateCategory.Proposed, StateCategory.Completed);
        var two = Of(StateCategory.Proposed, StateCategory.Removed, StateCategory.Removed);

        Assert.Throws<InvalidOperationException>(() => none.RemovedStateId);
        Assert.Throws<InvalidOperationException>(() => two.RemovedStateId);
        Assert.Empty(none.RemovedStateIds);
        Assert.Equal(2, two.RemovedStateIds.Count);
    }

    [Fact]
    public void IsRemoved_AgreesWith_Allows_ForEveryState()
    {
        var type = Of(StateCategory.Proposed, StateCategory.InProgress, StateCategory.Resolved, StateCategory.Completed, StateCategory.Removed);

        foreach (var state in type.States)
        {
            Assert.Equal(state.Category == StateCategory.Removed, type.IsRemoved(state.Id));
            Assert.Equal(state.Category != StateCategory.Removed, type.Allows(state.Id));
        }

        // A state of no type of this process is neither removed nor allowed.
        var stranger = Guid.CreateVersion7();

        Assert.False(type.IsRemoved(stranger));
        Assert.False(type.Allows(stranger));
        Assert.False(type.Allows(Guid.Empty));
    }

    // mod-005 code review CR-5: the list and the question are one definition. A type with none, one or two Removed states: for every
    // state of the type and for a stranger, "is in RemovedStateIds" is "IsRemoved", and "Allows" is "a state of the type, not removed".
    [Theory]
    [InlineData(new[] { StateCategory.Proposed, StateCategory.Completed })]
    [InlineData(new[] { StateCategory.Proposed, StateCategory.InProgress, StateCategory.Completed, StateCategory.Removed })]
    [InlineData(new[] { StateCategory.Proposed, StateCategory.Removed, StateCategory.Resolved, StateCategory.Removed })]
    public void RemovedStateIds_AndIsRemoved_AreOneDefinition_ForEveryState(StateCategory[] categories)
    {
        var type = Of(categories);
        var removed = type.RemovedStateIds;

        foreach (var state in type.States)
        {
            Assert.Equal(removed.Contains(state.Id), type.IsRemoved(state.Id));
            Assert.Equal(!removed.Contains(state.Id), type.Allows(state.Id));
        }

        Assert.Equal(type.States.Count(state => state.Category == StateCategory.Removed), removed.Count);
        Assert.DoesNotContain(Guid.CreateVersion7(), removed);
        Assert.False(type.IsRemoved(Guid.CreateVersion7()));
    }

    [Fact]
    public void FirstStateId_IsTheInitialState()
    {
        var type = Of(StateCategory.Proposed, StateCategory.Completed, StateCategory.Removed);

        Assert.Equal(type.States.Single(state => state.Position == StateDefinition.InitialPosition).Id, type.FirstStateId);
        Assert.Equal(type.InitialStateId, type.FirstStateId);
    }

    // ADR-0030 rule 2: the hierarchy's copy of "deleted" is built from the type's own list, so both agree for every state.
    [Fact]
    public void HierarchyTypes_IsDeleted_AgreesWith_TypeStates_IsRemoved()
    {
        var type = Of(StateCategory.Proposed, StateCategory.InProgress, StateCategory.Resolved, StateCategory.Completed, StateCategory.Removed);
        var hierarchy = new HierarchyTypes([new HierarchyType(type.TypeId, type.TypeKey, 2, type.RemovedStateIds)]);
        var projectId = Guid.CreateVersion7();

        foreach (var state in type.States)
        {
            var item = WorkItem.Create(
                projectId, 1, type, Guid.CreateVersion7(), "Заголовок", Guid.CreateVersion7(), DateTimeOffset.UnixEpoch, "creator-sub");

            if (state.Category == StateCategory.Removed)
            {
                item.Delete(type, "deleter-sub", DateTimeOffset.UnixEpoch);
            }
            else if (state.Id != type.InitialStateId)
            {
                item.Save(new WorkItemEdit(StateId: state.Id), type, DateTimeOffset.UnixEpoch);
            }

            Assert.Equal(item.IsDeleted(type), hierarchy.IsDeleted(item));
            Assert.Equal(state.Category == StateCategory.Removed, hierarchy.IsDeleted(item));
        }
    }

    // The seeded process: every type has exactly one Removed state, the last — what Delete relies on.
    [Fact]
    public void EverySeededType_HasOneRemovedState_TheLast()
    {
        var process = ProcessDefinition.Seed(new ProcessId(Guid.CreateVersion7()), Guid.CreateVersion7());

        foreach (var type in process.WorkItemTypes)
        {
            var states = new WorkItemTypeStates(
                type.Id,
                type.Key,
                type.Appearance,
                type.States.Select(state => new WorkItemTypeState(state.Id, state.Position, state.Category)).ToList());

            Assert.Equal(type.States.OrderBy(state => state.Position).Last().Id, states.RemovedStateId);
            Assert.False(states.Allows(states.RemovedStateId));
        }
    }
}
