using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Tests.WorkItems;

/// <summary>
/// mod-005's aggregate rules, without the database or the endpoint: Delete moves an item into its type's «Удалена» and
/// records the way back on the row (BR-DR-1..-3), Restore takes it out (BR-DR-10), and a save of a deleted item is refused
/// before any value is looked at (BR-DR-8, ADR-0030 rule 3).
/// </summary>
public sealed class WorkItemDeletionTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Deleted = Created.AddHours(2);
    private static readonly DateTimeOffset Restored = Created.AddHours(5);
    private static readonly DateTimeOffset Redeleted = Created.AddHours(8);

    private static readonly Guid ProjectId = Guid.CreateVersion7();

    private static readonly Guid NewId = Guid.CreateVersion7();
    private static readonly Guid ActiveId = Guid.CreateVersion7();
    private static readonly Guid ResolvedId = Guid.CreateVersion7();
    private static readonly Guid ClosedId = Guid.CreateVersion7();
    private static readonly Guid RemovedId = Guid.CreateVersion7();

    private static readonly WorkItemTypeStates Story = new(
        Guid.CreateVersion7(),
        "US",
        AppearanceToken.Story,
        [
            new WorkItemTypeState(NewId, 1, StateCategory.Proposed),
            new WorkItemTypeState(ActiveId, 2, StateCategory.InProgress),
            new WorkItemTypeState(ResolvedId, 3, StateCategory.Resolved),
            new WorkItemTypeState(ClosedId, 4, StateCategory.Completed),
            new WorkItemTypeState(RemovedId, 5, StateCategory.Removed),
        ]);

    /// <summary>A task has no «Разрешена» (its states are not a story's): the choice is limited to the item's own type.</summary>
    private static readonly WorkItemTypeStates Task = new(
        Guid.CreateVersion7(),
        "TASK",
        AppearanceToken.Task,
        [
            new WorkItemTypeState(Guid.CreateVersion7(), 1, StateCategory.Proposed),
            new WorkItemTypeState(Guid.CreateVersion7(), 2, StateCategory.Completed),
            new WorkItemTypeState(Guid.CreateVersion7(), 3, StateCategory.Removed),
        ]);

    private static WorkItem AStory(int number = 12) =>
        WorkItem.Create(ProjectId, number, Story, "Вход по ссылке", Guid.CreateVersion7(), Created, "creator-sub");

    /// <summary>The item moved to <paramref name="stateId"/> by a save, as a user would.</summary>
    private static WorkItem InState(Guid stateId)
    {
        var item = AStory();

        if (stateId != NewId)
        {
            item.Save(new WorkItemEdit(StateId: stateId), Story, Created);
        }

        return item;
    }

    /// <summary>An item in «Удалена» with no marks, as only a direct write can leave it (BR-DR-10).</summary>
    private static WorkItem InRemovedWithNoMarks()
    {
        var item = AStory();
        typeof(WorkItem).GetProperty(nameof(WorkItem.StateId))!.SetValue(item, RemovedId);

        return item;
    }

    private static void AssertNoMarks(WorkItem item)
    {
        Assert.Null(item.DeletedFromStateId);
        Assert.Null(item.DeletedBy);
        Assert.Null(item.DeletedAt);
    }

    // US-1 · "Confirming moves the item into «Удалена»" · "who and when" · «Изменён» takes the deletion's time (BR-DR-1, BR-DR-2).
    [Fact]
    public void Delete_LiveItem_MovesToTheRemovedState_RecordsHeldStateWhoAndWhen_ChangedAtIsTheDeletion()
    {
        var item = InState(ActiveId);

        item.Delete(Story, "deleter-sub", Deleted);

        Assert.Equal(RemovedId, item.StateId);
        Assert.Equal(ActiveId, item.DeletedFromStateId);
        Assert.Equal("deleter-sub", item.DeletedBy);
        Assert.Equal(Deleted, item.DeletedAt);
        Assert.Equal(Deleted, item.ChangedAt);
        Assert.True(item.IsDeleted(Story));
    }

    // US-1 · "An item is deleted from any non-Removed state": each one is remembered.
    [Theory]
    [MemberData(nameof(NonRemovedStates))]
    public void Delete_FromEveryNonRemovedState_RemembersIt(Guid stateId)
    {
        var item = InState(stateId);

        item.Delete(Story, "deleter-sub", Deleted);

        Assert.Equal(stateId, item.DeletedFromStateId);
        Assert.Equal(RemovedId, item.StateId);
    }

    public static TheoryData<Guid> NonRemovedStates() => new() { NewId, ActiveId, ResolvedId, ClosedId };

    // US-1 · "A deletion changes nothing else" (BR-DR-3): every other property compared before and after.
    [Fact]
    public void Delete_ChangesNothingElse()
    {
        var team =Guid.CreateVersion7();
        var item = WorkItem.Create(ProjectId, 13, Story, "Вход по ссылке", Guid.CreateVersion7(), Created, "creator-sub", team);
        item.Save(
            new WorkItemEdit(
                Description: "Описание",
                Assignee: new AssigneeEdit("assignee-sub", IsAvailable: true),
                Fields: new WorkItemFieldsEdit { Priority = new FieldEdit<int?>(1) }),
            Story,
            Created);

        var before = Snapshot(item);

        item.Delete(Story, "deleter-sub", Deleted);

        var after = Snapshot(item);

        foreach (var (name, value) in before)
        {
            if (name is nameof(WorkItem.StateId) or nameof(WorkItem.ChangedAt) or nameof(WorkItem.DeletedFromStateId)
                or nameof(WorkItem.DeletedBy) or nameof(WorkItem.DeletedAt))
            {
                continue;
            }

            Assert.True(Equals(value, after[name]), $"{name} changed by a deletion.");
        }

        Assert.Equal(team, item.OwningTeamId);
        Assert.Equal("assignee-sub", item.AssigneeId);
    }

    private static Dictionary<string, object?> Snapshot(WorkItem item) =>
        typeof(WorkItem).GetProperties()
            .Where(property => property.GetIndexParameters().Length == 0 && property.Name != nameof(WorkItem.AcceptanceCriteria))
            .ToDictionary(property => property.Name, property => property.GetValue(item));

    // US-1 · "Deleting something already deleted" (BR-DR-11): the second is refused and the first deletion's record stays.
    [Fact]
    public void Delete_AlreadyDeleted_ThrowsDeleted_NothingChanged()
    {
        var item = InState(ActiveId);
        item.Delete(Story, "first-sub", Deleted);

        var exception = Assert.Throws<ConflictException>(() => item.Delete(Story, "second-sub", Redeleted));

        Assert.Equal(WorkItemErrorCodes.WorkItemDeleted, exception.Code);
        Assert.Equal(ActiveId, item.DeletedFromStateId);
        Assert.Equal("first-sub", item.DeletedBy);
        Assert.Equal(Deleted, item.DeletedAt);
        Assert.Equal(Deleted, item.ChangedAt);
        Assert.Equal(RemovedId, item.StateId);
    }

    [Fact]
    public void Delete_WrongType_Throws()
    {
        var item = AStory();

        Assert.Throws<ArgumentException>(() => item.Delete(Task, "deleter-sub", Deleted));
        Assert.False(item.IsDeleted(Story));
        AssertNoMarks(item);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Delete_WithoutAnActor_Throws(string actor)
    {
        var item = AStory();

        Assert.ThrowsAny<ArgumentException>(() => item.Delete(Story, actor, Deleted));
        AssertNoMarks(item);
    }

    [Fact]
    public void Delete_ActorWiderThanTheColumn_Throws()
    {
        var item = AStory();

        Assert.Throws<ArgumentException>(() => item.Delete(Story, new string('x', WorkItemLimits.UserIdMaxLength + 1), Deleted));
        item.Delete(Story, new string('x', WorkItemLimits.UserIdMaxLength), Deleted);

        Assert.Equal(WorkItemLimits.UserIdMaxLength, item.DeletedBy!.Length);
    }

    // US-4 · "A second deletion remembers the latest state": the marks are anew after a restore.
    [Fact]
    public void Delete_TwiceWithARestoreBetween_RemembersTheLatestState()
    {
        var item = InState(ActiveId);
        item.Delete(Story, "first-sub", Deleted);
        item.Restore(Story, ClosedId, Restored);

        item.Delete(Story, "second-sub", Redeleted);

        Assert.Equal(ClosedId, item.DeletedFromStateId);
        Assert.Equal("second-sub", item.DeletedBy);
        Assert.Equal(Redeleted, item.DeletedAt);
    }

    // US-4 · "Accepting the proposal returns the item as it was": all three marks cleared, «Изменён» is the restore.
    [Fact]
    public void Restore_ToTheHeldState_ClearsAllThreeMarks_MovesChangedAt()
    {
        var item = InState(ResolvedId);
        item.Delete(Story, "deleter-sub", Deleted);

        item.Restore(Story, chosenStateId: null, Restored);

        Assert.Equal(ResolvedId, item.StateId);
        AssertNoMarks(item);
        Assert.Equal(Restored, item.ChangedAt);
        Assert.False(item.IsDeleted(Story));
    }

    // US-4 · "Any other state of the type may be chosen; the held state is forgotten".
    [Fact]
    public void Restore_ToAChosenState_ForgetsTheHeldOne()
    {
        var item = InState(ResolvedId);
        item.Delete(Story, "deleter-sub", Deleted);

        item.Restore(Story, ClosedId, Restored);

        Assert.Equal(ClosedId, item.StateId);
        AssertNoMarks(item);
    }

    // US-4 · "An item with no remembered state has a way back" (BR-DR-10).
    [Fact]
    public void Restore_ItemWithNoMarks_GoesToTheFirstState()
    {
        var item = InRemovedWithNoMarks();

        item.Restore(Story, chosenStateId: null, Restored);

        Assert.Equal(NewId, item.StateId);
        Assert.Equal(Restored, item.ChangedAt);
        AssertNoMarks(item);
    }

    // Plan D-P8: a held state the type does not allow any more (impossible in the MVP) falls back, never a 500.
    [Fact]
    public void Restore_HeldStateNotAllowedAnyMore_GoesToTheFirstState()
    {
        var item = InState(ResolvedId);
        item.Delete(Story, "deleter-sub", Deleted);

        var withoutResolved = new WorkItemTypeStates(
            Story.TypeId, Story.TypeKey, Story.Appearance, Story.States.Where(state => state.Id != ResolvedId).ToList());

        Assert.Equal(NewId, item.RestoreTarget(withoutResolved));

        item.Restore(withoutResolved, chosenStateId: null, Restored);

        Assert.Equal(NewId, item.StateId);
    }

    [Fact]
    public void RestoreTarget_IsTheHeldStateWhileTheTypeAllowsIt_ElseTheFirst()
    {
        var item = InState(ClosedId);
        item.Delete(Story, "deleter-sub", Deleted);

        Assert.Equal(ClosedId, item.RestoreTarget(Story));
        Assert.Equal(NewId, InRemovedWithNoMarks().RestoreTarget(Story));
    }

    // US-4 · "Restoring something not deleted is refused, even naming «Удалена» or a foreign state" (BR-DR-10 order).
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Restore_NotDeleted_ThrowsNotDeleted_BeforeStateNotAllowed(bool namesRemoved, bool namesForeign)
    {
        var item = InState(ActiveId);
        Guid? chosen = namesRemoved ? RemovedId : namesForeign ? Guid.CreateVersion7() : null;

        var exception = Assert.Throws<ConflictException>(() => item.Restore(Story, chosen, Restored));

        Assert.Equal(WorkItemErrorCodes.WorkItemNotDeleted, exception.Code);
        Assert.Equal(ActiveId, item.StateId);
        Assert.Equal(Created, item.ChangedAt);
    }

    // US-4 · "The choice is limited to the item's type": «Удалена», another type's state and an unknown id are refused.
    [Fact]
    public void Restore_StateOfAnotherTypeRemovedOrUnknown_StateNotAllowed_Unchanged()
    {
        var item = InState(ActiveId);
        item.Delete(Story, "deleter-sub", Deleted);

        foreach (var chosen in new[] { RemovedId, Task.InitialStateId, Guid.CreateVersion7() })
        {
            var exception = Assert.Throws<ConflictException>(() => item.Restore(Story, chosen, Restored));

            Assert.Equal(WorkItemErrorCodes.StateNotAllowed, exception.Code);
            Assert.Equal(RemovedId, item.StateId);
            Assert.Equal(ActiveId, item.DeletedFromStateId);
            Assert.Equal(Deleted, item.ChangedAt);
        }
    }

    // A value that named nothing (the handler maps a malformed id to Guid.Empty) is never allowed.
    [Fact]
    public void Restore_NamingNothing_GuidEmpty_IsStateNotAllowed()
    {
        var item = InState(ActiveId);
        item.Delete(Story, "deleter-sub", Deleted);

        var exception = Assert.Throws<ConflictException>(() => item.Restore(Story, Guid.Empty, Restored));

        Assert.Equal(WorkItemErrorCodes.StateNotAllowed, exception.Code);
        Assert.Equal(RemovedId, item.StateId);
    }

    // ADR-0030 rule 1: the three marks are all-or-none after every transition (the table's CHECK keeps them so).
    [Fact]
    public void Marks_AreAllOrNone_AfterEveryTransition()
    {
        var item = InState(ActiveId);
        AssertAllOrNone(item);

        item.Delete(Story, "deleter-sub", Deleted);
        AssertAllOrNone(item);
        Assert.NotNull(item.DeletedFromStateId);

        Assert.ThrowsAny<Exception>(() => item.Delete(Story, "other-sub", Redeleted));
        AssertAllOrNone(item);

        Assert.ThrowsAny<Exception>(() => item.Restore(Story, Guid.CreateVersion7(), Restored));
        AssertAllOrNone(item);

        item.Restore(Story, chosenStateId: null, Restored);
        AssertAllOrNone(item);
        Assert.Null(item.DeletedFromStateId);

        static void AssertAllOrNone(WorkItem value)
        {
            var set = new[] { value.DeletedFromStateId is not null, value.DeletedBy is not null, value.DeletedAt is not null };

            Assert.True(set.All(mark => mark) || set.All(mark => !mark));
        }
    }

    [Fact]
    public void EnsureLive_AndEnsureDeleted_AreEachOthersNegation()
    {
        var item = InState(ActiveId);

        item.EnsureLive(Story);
        Assert.Equal(WorkItemErrorCodes.WorkItemNotDeleted, Assert.Throws<ConflictException>(() => item.EnsureDeleted(Story)).Code);

        item.Delete(Story, "deleter-sub", Deleted);

        item.EnsureDeleted(Story);
        Assert.Equal(WorkItemErrorCodes.WorkItemDeleted, Assert.Throws<ConflictException>(() => item.EnsureLive(Story)).Code);
    }

    // US-3 · "The refusal comes before the value checks" and tech-012 m1: the stored state is judged before a live state
    // that comes with the edit, an unavailable assignee or a field the type lacks is looked at.
    [Fact]
    public void Save_OnADeletedItem_Throws_BeforeAnyFieldStateOrAssigneeIsLooked_At()
    {
        var item = InState(ActiveId);
        item.Delete(Story, "deleter-sub", Deleted);

        var edits = new[]
        {
            new WorkItemEdit(Title: "Другое"),
            new WorkItemEdit(StateId: ClosedId),
            new WorkItemEdit(StateId: Guid.CreateVersion7()),
            new WorkItemEdit(Assignee: new AssigneeEdit("somebody", IsAvailable: false)),
            new WorkItemEdit(Team: new TeamEdit(Guid.CreateVersion7())),
            new WorkItemEdit(StateId: ClosedId, Assignee: new AssigneeEdit("somebody", IsAvailable: false)),
        };

        foreach (var edit in edits)
        {
            var exception = Assert.Throws<ConflictException>(() => item.Save(edit, Story, Redeleted));

            Assert.Equal(WorkItemErrorCodes.WorkItemDeleted, exception.Code);
        }

        Assert.Equal(RemovedId, item.StateId);
        Assert.Equal("Вход по ссылке", item.Title);
        Assert.Equal(Deleted, item.ChangedAt);
    }

    [Fact]
    public void Save_OnADeletedItem_EmptyEdit_StillThrows()
    {
        var item = InState(ActiveId);
        item.Delete(Story, "deleter-sub", Deleted);

        var exception = Assert.Throws<ConflictException>(() => item.Save(new WorkItemEdit(), Story, Redeleted));

        Assert.Equal(WorkItemErrorCodes.WorkItemDeleted, exception.Code);
    }

    [Fact]
    public void Save_AfterARestore_Works()
    {
        var item = InState(ActiveId);
        item.Delete(Story, "deleter-sub", Deleted);
        item.Restore(Story, chosenStateId: null, Restored);

        Assert.True(item.Save(new WorkItemEdit(Title: "Новое"), Story, Redeleted));
        Assert.Equal("Новое", item.Title);
    }
}
