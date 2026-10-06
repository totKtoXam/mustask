using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Tests.WorkItems;

/// <summary>
/// The Parent/Child link's rules (feat-001 BR-H-1..-5, BR-H-8), without the
/// database or the endpoint: one decision for the form's save and the backlog's
/// mapping alike. The process is the seeded one's shape — «Эпик» rank 4,
/// «Фича» 3, «Пользовательская история» and «Баг» 2, «Задача» 1 — plus a second
/// epic type, <c>DEL</c>, whose items count as in «Удалена» (no public route
/// reaches that state, D-8 OQ-1).
/// </summary>
public sealed class WorkItemHierarchyTests
{
    private const string Key = "MSK";

    private static readonly DateTimeOffset Created = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Linked = Created.AddHours(1);
    private static readonly DateTimeOffset Later = Created.AddHours(2);
    private static readonly Guid ProjectId = Guid.CreateVersion7();
    private static readonly Guid RootAreaId = Guid.CreateVersion7();

    private static readonly WorkItemTypeStates Epic = Type("EPIC");
    private static readonly WorkItemTypeStates Feature = Type("FEAT");
    private static readonly WorkItemTypeStates Story = Type("US");
    private static readonly WorkItemTypeStates Bug = Type("BUG");
    private static readonly WorkItemTypeStates Task = Type("TASK");
    private static readonly WorkItemTypeStates DeletedEpic = Type("DEL");

    private static readonly HierarchyTypes Types = new(
    [
        Level(Epic, 4),
        Level(Feature, 3),
        Level(Story, 2),
        Level(Bug, 2),
        Level(Task, 1),
        new HierarchyType(DeletedEpic.TypeId, DeletedEpic.TypeKey, 4, [DeletedEpic.InitialStateId]),
    ]);

    private readonly Dictionary<Guid, WorkItem> _items = [];

    private static WorkItemTypeStates Type(string key) =>
        new(Guid.CreateVersion7(), key, [new WorkItemTypeState(Guid.CreateVersion7(), 1, StateCategory.Proposed), new WorkItemTypeState(Guid.CreateVersion7(), 2, StateCategory.Removed)]);

    /// <summary>A live type of the level: only its «Удалена» counts as deleted.</summary>
    private static HierarchyType Level(WorkItemTypeStates type, int rank) =>
        new(type.TypeId, type.TypeKey, rank, type.States.Where(state => state.Category == StateCategory.Removed).Select(state => state.Id).ToList());

    private WorkItem New(WorkItemTypeStates type, int number)
    {
        var item = WorkItem.Create(ProjectId, number, type, RootAreaId, $"Элемент {number}", Guid.CreateVersion7(), Created, "creator-sub");
        _items[item.Id] = item;

        return item;
    }

    /// <summary>The write as a handler builds it: the stored parent as loaded, the ancestry walked from the new parent.</summary>
    private ParentWrite Request(WorkItem child, WorkItem? newParent, Guid? seen)
    {
        var stored = child.ParentId is { } storedId ? _items[storedId] : null;
        var ancestry = new List<HierarchyLink>();

        for (var current = newParent; current is not null; current = current.ParentId is { } next ? _items[next] : null)
        {
            ancestry.Add(new HierarchyLink(current.Id, current.ParentId, current.TypeId, current.Number));

            if (ancestry.Count > _items.Count)
            {
                break;
            }
        }

        return new ParentWrite(child, stored, newParent, seen, ancestry);
    }

    private bool Write(WorkItem child, WorkItem? newParent, Guid? seen, DateTimeOffset? at = null) =>
        WorkItemHierarchy.Write(Request(child, newParent, seen), Types, Key, at ?? Later);

    /// <summary>A link made from an up-to-date view, at <see cref="Linked"/>.</summary>
    private void Link(WorkItem child, WorkItem parent) => Assert.True(Write(child, parent, child.ParentId, Linked));

    private static ConflictException Refused(Action write) => Assert.Throws<ConflictException>(write);

    // --- US-1 -------------------------------------------------------------------------------------------------------

    // US-1 · "A parent is set and saved" (BR-H-3, BR-H-5): the child holds the parent; «Изменён» moves on both.
    [Fact]
    public void Write_SetsTheParent_ChangedMovesOnTheChildAndTheParent()
    {
        var story = New(Story, 2);
        var feature = New(Feature, 6);

        Assert.True(Write(story, feature, seen: null));

        Assert.Equal(feature.Id, story.ParentId);
        Assert.Equal((Later, Later), (story.ChangedAt, feature.ChangedAt));
    }

    // US-1 · "Choosing another parent replaces the stored one in one save" (BR-H-3, OQ-2): exactly one parent, and the
    // «Изменён» times of the child, the old parent and the new one all move.
    [Fact]
    public void Write_AnotherParent_ReplacesIt_ChangedMovesOnAllThree()
    {
        var story = New(Story, 2);
        var oldParent = New(Feature, 6);
        var newParent = New(Feature, 8);
        Link(story, oldParent);

        Assert.True(Write(story, newParent, seen: oldParent.Id));

        Assert.Equal(newParent.Id, story.ParentId);
        Assert.Equal((Later, Later, Later), (story.ChangedAt, oldParent.ChangedAt, newParent.ChangedAt));
    }

    // US-1 · "«Нет родителя» clears the parent": no parent; the child and the old parent move.
    [Fact]
    public void Write_None_ClearsTheParent_ChangedMovesOnTheChildAndTheOldParent()
    {
        var story = New(Story, 2);
        var feature = New(Feature, 6);
        Link(story, feature);

        Assert.True(Write(story, null, seen: feature.Id));

        Assert.Null(story.ParentId);
        Assert.Equal((Later, Later), (story.ChangedAt, feature.ChangedAt));
    }

    // BR-H-3: the stored parent named again, or none where there is none, changes nothing, «Изменён» included.
    [Fact]
    public void Write_TheStoredParentAgain_ChangesNothing()
    {
        var story = New(Story, 2);
        var feature = New(Feature, 6);
        var lone = New(Bug, 4);
        Link(story, feature);

        Assert.False(Write(story, feature, seen: feature.Id));
        Assert.False(Write(lone, null, seen: null));

        Assert.Equal((Linked, Linked, Created), (story.ChangedAt, feature.ChangedAt, lone.ChangedAt));
    }

    // US-1 · "A link changes no other field and no backlog place" (BR-H-5): the state, the assignee, the area and the
    // title stay.
    [Fact]
    public void Write_ChangesNoOtherField()
    {
        var story = New(Story, 2);
        var before = (story.StateId, story.AssigneeId, story.AreaId, story.Title, story.Description);

        Write(story, New(Feature, 6), seen: null);

        Assert.Equal(before, (story.StateId, story.AssigneeId, story.AreaId, story.Title, story.Description));
    }

    // US-1 · "Each type is offered the parents its level allows" (BR-H-2): rank the child's own or higher.
    [Fact]
    public void ParentTypeIds_ByLevelRank_OwnOrHigher()
    {
        Assert.Equal(Ids(Epic, DeletedEpic), Sorted(Types.ParentTypeIds(Epic.TypeId)));
        Assert.Equal(Ids(Epic, DeletedEpic, Feature), Sorted(Types.ParentTypeIds(Feature.TypeId)));
        Assert.Equal(Ids(Epic, DeletedEpic, Feature, Story, Bug), Sorted(Types.ParentTypeIds(Bug.TypeId)));
        Assert.Equal(Ids(Epic, DeletedEpic, Feature, Story, Bug, Task), Sorted(Types.ParentTypeIds(Task.TypeId)));
        Assert.DoesNotContain(Task.TypeId, Types.ParentTypeIds(Story.TypeId));
    }

    // US-5 · the dialog offers only the level directly above (BR-H-14): an epic for a feature, a feature for a story or a
    // bug, a story or a bug for a task; nothing for an epic.
    [Fact]
    public void LevelAboveTypeIds_TheNextHigherLevelOnly()
    {
        Assert.Equal(Ids(Epic, DeletedEpic), Sorted(Types.LevelAboveTypeIds(Feature.TypeId)));
        Assert.Equal(Ids(Feature), Sorted(Types.LevelAboveTypeIds(Story.TypeId)));
        Assert.Equal(Ids(Feature), Sorted(Types.LevelAboveTypeIds(Bug.TypeId)));
        Assert.Equal(Ids(Story, Bug), Sorted(Types.LevelAboveTypeIds(Task.TypeId)));
        Assert.Empty(Types.LevelAboveTypeIds(Epic.TypeId));
    }

    // --- US-2 -------------------------------------------------------------------------------------------------------

    // US-2 · "A parent on a lower level is refused" and "A refused replace keeps the old parent" (BR-H-2, BR-H-3).
    [Fact]
    public void Write_LowerLevel_Refused_TheOldParentStays()
    {
        var story = New(Story, 2);
        var feature = New(Feature, 6);
        var task = New(Task, 3);
        var lone = New(Story, 5);
        Link(story, feature);

        Assert.Equal(WorkItemErrorCodes.ParentLevelNotAllowed, Refused(() => Write(story, task, seen: feature.Id)).Code);
        Assert.Equal(WorkItemErrorCodes.ParentLevelNotAllowed, Refused(() => Write(lone, task, seen: null)).Code);

        Assert.Equal((feature.Id, Linked, Linked), (story.ParentId, story.ChangedAt, feature.ChangedAt));
        Assert.Equal((null, Created), (lone.ParentId, task.ChangedAt));
    }

    // US-2 · "Same level and skipped levels are allowed" (doc-002 W-1): a bug under a story, a task under an epic.
    [Fact]
    public void Write_SameLevelAndSkippedLevels_Allowed()
    {
        var bug = New(Bug, 4);
        var story = New(Story, 2);
        var task = New(Task, 3);
        var epic = New(Epic, 1);

        Assert.True(Write(bug, story, seen: null));
        Assert.True(Write(task, epic, seen: null));

        Assert.Equal((story.Id, epic.Id), (bug.ParentId, task.ParentId));
    }

    // US-2 · "An item is not its own parent".
    [Fact]
    public void Write_Itself_RefusedAsSelfParent()
    {
        var feature = New(Feature, 6);

        Assert.Equal(WorkItemErrorCodes.SelfParent, Refused(() => Write(feature, feature, seen: null)).Code);
        Assert.Null(feature.ParentId);
    }

    // US-2 · "A cycle is refused, naming its path": MSK-TASK-12 as the parent of MSK-TASK-10, where 10 → 11 → 12.
    [Fact]
    public void Write_ADescendant_RefusedAsCycle_WithThePathFromTheItemDownToTheParent()
    {
        var task10 = New(Task, 10);
        var task11 = New(Task, 11);
        var task12 = New(Task, 12);
        Link(task11, task10);
        Link(task12, task11);

        var refusal = Refused(() => Write(task10, task12, seen: null));

        Assert.Equal(WorkItemErrorCodes.Cycle, refusal.Code);
        Assert.Equal("MSK-TASK-12", refusal.Params["parent"].Value);
        Assert.Equal("MSK-TASK-10", refusal.Params["item"].Value);
        Assert.Equal("MSK-TASK-10 → MSK-TASK-11 → MSK-TASK-12", refusal.Params["path"].Value);
        Assert.Equal((null, task10.Id, task11.Id), (task10.ParentId, task11.ParentId, task12.ParentId));
    }

    // US-2 · the direct child named as the parent is a cycle of two.
    [Fact]
    public void Write_TheDirectChild_RefusedAsCycleOfTwo()
    {
        var story = New(Story, 2);
        var bug = New(Bug, 4);
        Link(bug, story);

        var refusal = Refused(() => Write(story, bug, seen: null));

        Assert.Equal(WorkItemErrorCodes.Cycle, refusal.Code);
        Assert.Equal("MSK-US-2 → MSK-BUG-4", refusal.Params["path"].Value);
    }

    // US-2 · "A parent chosen from a stale view is refused" and "Two users map the same child at once, the later is
    // refused" (BR-H-4): the write names the parent its user saw; another stored parent refuses it.
    [Fact]
    public void Write_FromAStaleView_RefusedAsParentChanged_NothingChanges()
    {
        var story = New(Story, 2);
        var anna = New(Feature, 6);
        var boris = New(Feature, 8);
        Link(story, boris);

        Assert.Equal(WorkItemErrorCodes.ParentChanged, Refused(() => Write(story, anna, seen: null)).Code);
        Assert.Equal(WorkItemErrorCodes.ParentChanged, Refused(() => Write(story, null, seen: anna.Id)).Code);
        Assert.Equal(WorkItemErrorCodes.ParentChanged, Refused(() => Write(story, anna, seen: Guid.Empty)).Code);

        Assert.Equal((boris.Id, Linked, Created), (story.ParentId, story.ChangedAt, anna.ChangedAt));
    }

    // --- US-7 -------------------------------------------------------------------------------------------------------

    // US-7 · "No link is written to a deleted parent" (BR-H-8): the old link stays.
    [Fact]
    public void Write_ADeletedNewParent_RefusedAsItemDeleted()
    {
        var feature = New(Feature, 23);
        var parent = New(Feature, 8);
        var deleted = New(DeletedEpic, 20);
        Link(feature, parent);

        Assert.Equal(WorkItemErrorCodes.ItemDeleted, Refused(() => Write(feature, deleted, seen: parent.Id)).Code);
        Assert.Equal(parent.Id, feature.ParentId);
    }

    // US-7 · "A deleted child takes no link write": set, replace or clear.
    [Fact]
    public void Write_ADeletedChild_RefusedAsItemDeleted_WhateverTheWrite()
    {
        var types = new HierarchyTypes(
        [
            Level(Epic, 4),
            new HierarchyType(Feature.TypeId, Feature.TypeKey, 3, [Feature.InitialStateId]),
        ]);
        var feature = New(Feature, 23);
        var parent = New(Epic, 1);
        var other = New(Epic, 20);
        Link(feature, parent);

        foreach (var (newParent, seen) in new (WorkItem?, Guid?)[] { (other, parent.Id), (null, parent.Id), (parent, parent.Id) })
        {
            var refusal = Refused(() => WorkItemHierarchy.Write(Request(feature, newParent, seen), types, Key, Later));
            Assert.Equal(WorkItemErrorCodes.ItemDeleted, refusal.Code);
        }

        Assert.Equal((parent.Id, Linked), (feature.ParentId, feature.ChangedAt));
    }

    // US-7 · "The mapping replaces the link to a deleted parent" and "The child's form lets go of a deleted parent"
    // (BR-H-4, BR-H-8): a write that saw none matches a stored parent in «Удалена»; the deleted parent is left exactly
    // as it was, «Изменён» included.
    [Fact]
    public void Write_SawNone_StoredParentDeleted_Matches_TheDeletedParentStaysUntouched()
    {
        var replaced = New(Feature, 6);
        var cleared = New(Feature, 8);
        var deleted = New(DeletedEpic, 1);
        var epic = New(Epic, 20);

        // The deleted epic held both features before its deletion: the links are made on a process where it was live.
        var live = new HierarchyTypes([Level(Epic, 4), Level(Feature, 3), new HierarchyType(DeletedEpic.TypeId, DeletedEpic.TypeKey, 4, [])]);
        Assert.True(WorkItemHierarchy.Write(Request(replaced, deleted, seen: null), live, Key, Linked));
        Assert.True(WorkItemHierarchy.Write(Request(cleared, deleted, seen: null), live, Key, Linked));

        Assert.True(Write(replaced, epic, seen: null));
        Assert.True(Write(cleared, null, seen: null));

        Assert.Equal((epic.Id, (Guid?)null), (replaced.ParentId, cleared.ParentId));
        Assert.Equal((Later, Later, Later), (replaced.ChangedAt, cleared.ChangedAt, epic.ChangedAt));
        Assert.Equal(Linked, deleted.ChangedAt);
    }

    // US-7 · the deleted stored parent named as seen matches too (the form showed it with «Удалена»).
    [Fact]
    public void Write_SawTheDeletedParent_Matches()
    {
        var feature = New(Feature, 6);
        var deleted = New(DeletedEpic, 1);
        var live = new HierarchyTypes([Level(Epic, 4), Level(Feature, 3), new HierarchyType(DeletedEpic.TypeId, DeletedEpic.TypeKey, 4, [])]);
        Assert.True(WorkItemHierarchy.Write(Request(feature, deleted, seen: null), live, Key, Linked));

        Assert.True(Write(feature, null, seen: deleted.Id));

        Assert.Null(feature.ParentId);
        Assert.Equal(Linked, deleted.ChangedAt);
    }

    // --- The order of the checks (api-contract § 3.2) ----------------------------------------------------------------

    // Deleted before stale, stale before self, self before level, level before cycle.
    [Fact]
    public void Write_SeveralFaults_TheFirstInTheSpecsOrderAnswers()
    {
        var story = New(Story, 2);
        var feature = New(Feature, 6);
        var deleted = New(DeletedEpic, 1);
        var task = New(Task, 3);
        Link(story, feature);

        Assert.Equal(WorkItemErrorCodes.ItemDeleted, Refused(() => Write(story, deleted, seen: null)).Code);
        Assert.Equal(WorkItemErrorCodes.ParentChanged, Refused(() => Write(story, story, seen: null)).Code);
        Assert.Equal(WorkItemErrorCodes.SelfParent, Refused(() => Write(task, task, seen: null)).Code);
        Assert.Equal(WorkItemErrorCodes.ParentLevelNotAllowed, Refused(() => Write(story, task, seen: feature.Id)).Code);
    }

    // BR-H-1: the caller hands in a consistent write — the child's own stored parent, items of one project; anything
    // else is a defect, not an answer.
    [Fact]
    public void Write_InconsistentInput_IsADefect()
    {
        var story = New(Story, 2);
        var feature = New(Feature, 6);
        var elsewhere = WorkItem.Create(Guid.CreateVersion7(), 1, Feature, RootAreaId, "Чужая", Guid.CreateVersion7(), Created, "sub");
        _items[elsewhere.Id] = elsewhere;

        Assert.Throws<ArgumentException>(() => WorkItemHierarchy.Write(new ParentWrite(story, feature, null, null, []), Types, Key, Later));
        Assert.Throws<ArgumentException>(() => WorkItemHierarchy.Write(Request(story, elsewhere, null), Types, Key, Later));
        Assert.Throws<ArgumentException>(() => WorkItemHierarchy.Write(new ParentWrite(story, null, feature, null, []), Types, Key, Later));
    }

    private static Guid[] Ids(params WorkItemTypeStates[] types) => types.Select(type => type.TypeId).Order().ToArray();

    private static Guid[] Sorted(IEnumerable<Guid> ids) => ids.Order().ToArray();
}
