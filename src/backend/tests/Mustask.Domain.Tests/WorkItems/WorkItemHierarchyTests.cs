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

    private static readonly WorkItemTypeStates Epic = Type("EPIC", AppearanceToken.Epic);
    private static readonly WorkItemTypeStates Feature = Type("FEAT", AppearanceToken.Feature);
    private static readonly WorkItemTypeStates Story = Type("US", AppearanceToken.Story);
    private static readonly WorkItemTypeStates Bug = Type("BUG", AppearanceToken.Bug);
    private static readonly WorkItemTypeStates Task = Type("TASK", AppearanceToken.Task);
    private static readonly WorkItemTypeStates DeletedEpic = Type("DEL", AppearanceToken.Epic);

    /// <summary>A story type of the middle level whose items count as in «Удалена», for the orders that need a deleted parent of a LOWER level.</summary>
    private static readonly WorkItemTypeStates DeletedStory = Type("DELS", AppearanceToken.Story);

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

    private static WorkItemTypeStates Type(string key, AppearanceToken appearance) =>
        new(Guid.CreateVersion7(), key, appearance, [new WorkItemTypeState(Guid.CreateVersion7(), 1, StateCategory.Proposed), new WorkItemTypeState(Guid.CreateVersion7(), 2, StateCategory.Removed)]);

    /// <summary>A live type of the level: only its «Удалена» counts as deleted.</summary>
    private static HierarchyType Level(WorkItemTypeStates type, int rank) =>
        new(type.TypeId, type.TypeKey, rank, type.States.Where(state => state.Category == StateCategory.Removed).Select(state => state.Id).ToList());

    private WorkItem New(WorkItemTypeStates type, int number)
    {
        var item = WorkItem.Create(ProjectId, number, type, RootAreaId, $"Элемент {number}", Guid.CreateVersion7(), Created, "creator-sub");
        _items[item.Id] = item;

        return item;
    }

    /// <summary>
    /// The write as a handler builds it: the stored parent as loaded, the ancestry walked from the new parent, the
    /// height of the child's own subtree over the stored links.
    /// </summary>
    private ParentWrite Request(WorkItem child, WorkItem? newParent, Guid? seen)
    {
        var stored = child.ParentId is { } storedId ? _items[storedId] : null;

        return new ParentWrite(child, stored, newParent, seen, AncestryOf(newParent), HeightOf(child));
    }

    /// <summary>The item and every ancestor of it as stored, as the repository's recursive statement answers.</summary>
    private List<HierarchyLink> AncestryOf(WorkItem? start)
    {
        var ancestry = new List<HierarchyLink>();

        for (var current = start; current is not null; current = current.ParentId is { } next ? _items[next] : null)
        {
            ancestry.Add(new HierarchyLink(current.Id, current.ParentId, current.TypeId, current.Number));

            if (ancestry.Count > _items.Count)
            {
                break;
            }
        }

        return ancestry;
    }

    /// <summary>The number of items on the longest chain from the item down, itself counted (1 for a leaf).</summary>
    private int HeightOf(WorkItem item)
    {
        var childrenOf = _items.Values.Where(other => other.ParentId is not null).ToLookup(other => other.ParentId!.Value);
        var (height, level) = (0, new List<WorkItem> { item });

        while (level.Count > 0)
        {
            height++;
            level = level.SelectMany(parent => childrenOf[parent.Id]).ToList();
        }

        return height;
    }

    /// <summary>
    /// A chain of <paramref name="length"/> tasks, each beneath the one before, made by the parent write itself; the
    /// numbers run from <paramref name="firstNumber"/>. Returns the chain from its top item down.
    /// </summary>
    private List<WorkItem> Chain(int length, int firstNumber = 100)
    {
        var chain = new List<WorkItem>();

        for (var index = 0; index < length; index++)
        {
            var item = New(Task, firstNumber + index);

            if (chain.Count > 0)
            {
                Link(item, chain[^1]);
            }

            chain.Add(item);
        }

        return chain;
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

        Assert.Throws<ArgumentException>(() => WorkItemHierarchy.Write(new ParentWrite(story, feature, null, null, [], 1), Types, Key, Later));
        Assert.Throws<ArgumentException>(() => WorkItemHierarchy.Write(Request(story, elsewhere, null), Types, Key, Later));
        Assert.Throws<ArgumentException>(() => WorkItemHierarchy.Write(new ParentWrite(story, null, feature, null, [], 1), Types, Key, Later));
        Assert.Throws<ArgumentException>(() => WorkItemHierarchy.Write(new ParentWrite(story, null, feature, null, AncestryOf(feature), 0), Types, Key, Later));
    }

    // --- feat-027 D-9: the depth bound -------------------------------------------------------------------------------

    // D-9 · a link that makes a chain of exactly 64 items is accepted, one that makes 65 is refused: a task beneath the
    // 63rd and the 64th item of a chain of tasks (the parent's chain of ancestors, itself included, plus the new item).
    [Fact]
    public void Write_AChainOfExactlyTheBound_Accepted_OneMoreRefused()
    {
        var chain = Chain(WorkItemHierarchy.MaxChainLength - 1);
        var last = New(Task, 500);
        var over = New(Task, 501);

        Assert.True(Write(last, chain[^1], seen: null));
        var refused = Refused(() => Write(over, last, seen: null));

        Assert.Equal(64, WorkItemHierarchy.MaxChainLength);
        Assert.Equal(WorkItemErrorCodes.DepthExceeded, refused.Code);
        Assert.Equal(64m, refused.Params["limit"].Value);
        Assert.Equal((chain[^1].Id, (Guid?)null), (last.ParentId, over.ParentId));
        Assert.Equal((Later, Created), (last.ChangedAt, over.ChangedAt));
    }

    // D-9 · the moved item's own subtree counts: the parent's chain of ancestors plus the subtree's height. A subtree of
    // 24 items (a chain) fits beneath a parent whose chain holds 40, and does not beneath one whose chain holds 41.
    [Theory]
    [InlineData(40, 24, true)]
    [InlineData(41, 24, false)]
    [InlineData(1, 63, true)]
    [InlineData(1, 64, false)]
    [InlineData(63, 1, true)]
    [InlineData(64, 1, false)]
    public void Write_TheMovedSubtreeCounts_ParentChainPlusSubtreeHeight(int parentChain, int subtreeHeight, bool accepted)
    {
        var parent = Chain(parentChain, firstNumber: 1000)[^1];
        var moved = Chain(subtreeHeight, firstNumber: 2000)[0];
        var parentChangedBefore = parent.ChangedAt;

        if (accepted)
        {
            Assert.True(Write(moved, parent, seen: null));
            Assert.Equal(parent.Id, moved.ParentId);
        }
        else
        {
            Assert.Equal(WorkItemErrorCodes.DepthExceeded, Refused(() => Write(moved, parent, seen: null)).Code);
            Assert.Null(moved.ParentId);
            Assert.Equal(parentChangedBefore, parent.ChangedAt);
        }
    }

    // D-9 · replacing a parent is checked the same: the moved subtree's height against the new parent's chain, not the
    // old one's.
    [Fact]
    public void Write_ReplacingTheParent_ChecksTheNewParentsChain()
    {
        var deep = Chain(64, firstNumber: 1000)[^1];
        var shallow = New(Task, 3000);
        var moved = New(Task, 3001);
        Link(moved, shallow);

        var refused = Refused(() => Write(moved, deep, seen: shallow.Id));

        Assert.Equal(WorkItemErrorCodes.DepthExceeded, refused.Code);
        Assert.Equal((shallow.Id, Linked), (moved.ParentId, moved.ChangedAt));
    }

    // D-9 · a clear and the stored parent named again are not depth checks: no chain gets longer, so a subtree stored
    // deeper than the bound before it (no write makes one now) can still be taken out of its parent.
    [Fact]
    public void Write_AClearAndTheStoredParentAgain_AreNotCheckedForDepth()
    {
        var parent = New(Task, 1);
        var child = New(Task, 2);
        Link(child, parent);
        var tooDeepStored = 200;

        var cleared = WorkItemHierarchy.Write(new ParentWrite(child, parent, null, parent.Id, [], tooDeepStored), Types, Key, Later);
        Link(child, parent);
        var again = WorkItemHierarchy.Write(new ParentWrite(child, parent, parent, parent.Id, AncestryOf(parent), tooDeepStored), Types, Key, Later);

        Assert.True(cleared);
        Assert.False(again);
    }

    // D-9 · the order: the cycle is answered before the depth (the more specific fault), and the level, the stale view
    // and a deleted item before both.
    [Fact]
    public void Write_DepthComesAfterTheOtherChecks()
    {
        var chain = Chain(64);
        var lowerLevel = New(Story, 600);
        var oneMore = New(Task, 601);
        var stale = New(Task, 602);
        Link(stale, oneMore);

        // The top of a full chain beneath its own tail: a cycle, although it would be too deep as well.
        Assert.Equal(WorkItemErrorCodes.Cycle, Refused(() => Write(chain[0], chain[^1], seen: null)).Code);
        // A story beneath the chain's tail: the level, although the chain is full.
        Assert.Equal(WorkItemErrorCodes.ParentLevelNotAllowed, Refused(() => Write(lowerLevel, chain[^1], seen: null)).Code);
        // A stale view beneath a full chain: the stale view first.
        Assert.Equal(WorkItemErrorCodes.ParentChanged, Refused(() => Write(stale, chain[^1], seen: null)).Code);
        // And at last, only the depth.
        Assert.Equal(WorkItemErrorCodes.DepthExceeded, Refused(() => Write(oneMore, chain[^1], seen: null)).Code);
    }

    // --- feat-027: a child created from a row (BR-T-10) ------------------------------------------------------------

    private WorkItem Child(WorkItem parent, WorkItemTypeStates type, int number) =>
        WorkItemHierarchy.CreateChild(parent, AncestryOf(parent), Types, number, type, "Повторная отправка письма", Guid.CreateVersion7(), Later, "creator-sub", null);

    // feat-027 US-4 · "The child takes the parent's area, state and defaults": a story created beneath MSK-FEAT-6 of
    // «Платформа / API» is in that area, in its initial state, unassigned, with feat-022's defaults, MSK-FEAT-6 its
    // parent; its «Изменён» is its creation time and MSK-FEAT-6's moves to the same time.
    [Fact]
    public void CreateChild_TakesTheParentsArea_LinksIt_AndMovesTheParentsChanged()
    {
        var apiArea = Guid.CreateVersion7();
        var feature = WorkItem.Create(ProjectId, 6, Feature, apiArea, "Вход по ссылке", Guid.CreateVersion7(), Created, "creator-sub");

        var story = Child(feature, Story, 24);

        Assert.Equal((ProjectId, 24, Story.TypeId, apiArea), (story.ProjectId, story.Number, story.TypeId, story.AreaId));
        Assert.Equal((Story.InitialStateId, (string?)null, 2), (story.StateId, story.AssigneeId, story.Priority));
        Assert.Equal(WorkItemValueArea.Business, story.ValueArea);
        Assert.Equal(feature.Id, story.ParentId);
        Assert.Equal((Later, Later, Later), (story.CreatedAt, story.ChangedAt, feature.ChangedAt));
        Assert.Equal((apiArea, Feature.InitialStateId, (Guid?)null), (feature.AreaId, feature.StateId, feature.ParentId));
    }

    // mod-017 US-2 · "a child starts with its parent's team" · "a child may have another team" (BR-OT-1): the child gets the
    // team the caller decided; the parent keeps its own, and no child follows its parent's team afterwards.
    [Fact]
    public void CreateChild_TakesTheTeamTheCallerDecided_AndTheParentKeepsItsOwn()
    {
        var parentTeam = Guid.CreateVersion7();
        var childTeam = Guid.CreateVersion7();
        var feature = WorkItem.Create(ProjectId, 6, Feature, Guid.CreateVersion7(), "Вход по ссылке", Guid.CreateVersion7(), Created, "creator-sub", parentTeam);

        var story = WorkItemHierarchy.CreateChild(
            feature, AncestryOf(feature), Types, 24, Story, "Повторная отправка письма", Guid.CreateVersion7(), Later, "creator-sub", childTeam);
        var teamless = WorkItemHierarchy.CreateChild(
            feature, AncestryOf(feature), Types, 25, Story, "Без команды", Guid.CreateVersion7(), Later, "creator-sub", null);

        Assert.Equal(childTeam, story.OwningTeamId);
        Assert.Null(teamless.OwningTeamId);
        Assert.Equal(parentTeam, feature.OwningTeamId);
    }

    // feat-027 US-4 · each row's entries (BR-T-9) are pairs BR-H-2 allows — a feature beneath an epic, a story or a bug
    // beneath a feature, a task or a bug beneath a story — and so is a child of the parent's own level or one skipping
    // levels. The pair a task beneath a bug is no row entry (D-6 OQ-6 (b) withdrew it) but a BR-H-2 pair all the same, and
    // is kept here as the process's own rule.
    [Theory]
    [InlineData("EPIC", "FEAT")]
    [InlineData("FEAT", "US")]
    [InlineData("FEAT", "BUG")]
    [InlineData("US", "TASK")]
    [InlineData("US", "BUG")]
    [InlineData("BUG", "TASK")]
    [InlineData("FEAT", "FEAT")]
    [InlineData("EPIC", "TASK")]
    public void CreateChild_APairTheProcessAllows_IsLinked(string parentKey, string childKey)
    {
        var parent = New(TypeOf(parentKey), 1);

        Assert.Equal(parent.Id, Child(parent, TypeOf(childKey), 2).ParentId);
    }

    // feat-027 BR-T-10: a parent of a lower level is refused with `parent_level_not_allowed`, and the parent is
    // untouched.
    [Theory]
    [InlineData("US", "FEAT")]
    [InlineData("TASK", "BUG")]
    [InlineData("FEAT", "EPIC")]
    public void CreateChild_ALowerLevelParent_RefusedAsParentLevelNotAllowed(string parentKey, string childKey)
    {
        var parent = New(TypeOf(parentKey), 1);

        var refused = Assert.Throws<ConflictException>(() => Child(parent, TypeOf(childKey), 2));

        Assert.Equal(WorkItemErrorCodes.ParentLevelNotAllowed, refused.Code);
        Assert.Equal(Created, parent.ChangedAt);
    }

    // feat-027 US-4 · "A parent gone meanwhile refuses the create": a parent in «Удалена» is refused with
    // `item_deleted`. A deleted epic is of the top level, so the level check would allow a feature and an epic beneath it:
    // their refusal can only be the deleted state.
    [Fact]
    public void CreateChild_ADeletedParent_RefusedAsItemDeleted()
    {
        var deleted = New(DeletedEpic, 1);

        Assert.Equal(WorkItemErrorCodes.ItemDeleted, Assert.Throws<ConflictException>(() => Child(deleted, Feature, 2)).Code);
        Assert.Equal(WorkItemErrorCodes.ItemDeleted, Assert.Throws<ConflictException>(() => Child(deleted, Epic, 3)).Code);
        Assert.Equal(Created, deleted.ChangedAt);
    }

    // feat-027 US-4 · the order of CreateChild: «Удалена» before the level. A deleted parent of a LOWER level than the
    // child is one both checks would refuse — a feature beneath a deleted story — and `item_deleted` is the answer; with
    // the two checks swapped it would be `parent_level_not_allowed`.
    [Fact]
    public void CreateChild_ADeletedLowerLevelParent_RefusedAsItemDeleted_BeforeTheLevel()
    {
        var types = new HierarchyTypes(
        [
            Level(Feature, 3),
            new HierarchyType(DeletedStory.TypeId, DeletedStory.TypeKey, 2, [DeletedStory.InitialStateId]),
        ]);
        var deleted = New(DeletedStory, 1);
        var liveLowerLevel = New(Task, 4);

        Assert.False(WorkItemHierarchy.AllowsParent(types.Of(Feature.TypeId).LevelRank, types.Of(deleted.TypeId).LevelRank));
        var refused = Assert.Throws<ConflictException>(() =>
            WorkItemHierarchy.CreateChild(deleted, AncestryOf(deleted), types, 2, Feature, "Фича", Guid.CreateVersion7(), Later, "creator-sub", null));

        Assert.Equal(WorkItemErrorCodes.ItemDeleted, refused.Code);
        Assert.Equal(Created, deleted.ChangedAt);

        // The same pair on a live lower-level parent is the level's refusal, so the test tells the two answers apart.
        var levelOnly = new HierarchyTypes([Level(Feature, 3), Level(Task, 1)]);
        Assert.Equal(
            WorkItemErrorCodes.ParentLevelNotAllowed,
            Assert.Throws<ConflictException>(() =>
                WorkItemHierarchy.CreateChild(liveLowerLevel, AncestryOf(liveLowerLevel), levelOnly, 3, Feature, "Фича", Guid.CreateVersion7(), Later, "creator-sub", null)).Code);
    }

    // feat-027 D-9 · a child is the next item of its parent's chain: beneath the 63rd item of a chain it makes 64 and is
    // created, beneath the 64th it would make 65 and is refused with `depth_exceeded`, creating nothing.
    [Fact]
    public void CreateChild_BeneathTheBoundsLastLink_Created_BeneathTheBoundItself_Refused()
    {
        var chain = Chain(WorkItemHierarchy.MaxChainLength);

        var made = Child(chain[^2], Task, 700);
        var refused = Assert.Throws<ConflictException>(() => Child(chain[^1], Task, 701));

        Assert.Equal(chain[^2].Id, made.ParentId);
        Assert.Equal(WorkItemErrorCodes.DepthExceeded, refused.Code);
        Assert.Equal(64m, refused.Params["limit"].Value);
        Assert.Equal(Linked, chain[^1].ChangedAt);
    }

    // feat-027 D-9 · the order: a deleted parent and a parent of a lower level are answered before the depth, even at the
    // bound's end.
    [Fact]
    public void CreateChild_AtTheBound_DeletedAndLevelAreAnsweredBeforeTheDepth()
    {
        var types = new HierarchyTypes([Level(Task, 1), Level(Story, 2), new HierarchyType(DeletedStory.TypeId, DeletedStory.TypeKey, 2, [DeletedStory.InitialStateId])]);
        var chain = Chain(WorkItemHierarchy.MaxChainLength - 1);
        var deletedTail = New(DeletedStory, 800);
        var liveTail = New(Task, 801);

        // The links are stored straight: the process these two types belong to is the test's own, not `Types`.
        deletedTail.LinkTo(chain[^1].Id, Linked);
        liveTail.LinkTo(chain[^1].Id, Linked);

        // The deleted story is the 64th item of the chain; a task beneath it would be the 65th.
        Assert.Equal(
            WorkItemErrorCodes.ItemDeleted,
            Assert.Throws<ConflictException>(() =>
                WorkItemHierarchy.CreateChild(deletedTail, AncestryOf(deletedTail), types, 802, Task, "Задача", Guid.CreateVersion7(), Later, "creator-sub", null)).Code);
        // A story beneath the 64th item, a task: the level is not allowed, although it would be the 65th too.
        Assert.Equal(
            WorkItemErrorCodes.ParentLevelNotAllowed,
            Assert.Throws<ConflictException>(() =>
                WorkItemHierarchy.CreateChild(liveTail, AncestryOf(liveTail), types, 803, Story, "История", Guid.CreateVersion7(), Later, "creator-sub", null)).Code);
    }

    private static WorkItemTypeStates TypeOf(string key) =>
        new[] { Epic, Feature, Story, Bug, Task }.Single(type => type.TypeKey == key);

    private static Guid[] Ids(params WorkItemTypeStates[] types) => types.Select(type => type.TypeId).Order().ToArray();

    private static Guid[] Sorted(IEnumerable<Guid> ids) => ids.Order().ToArray();
}
