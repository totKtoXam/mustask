using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Tests.WorkItems;

/// <summary>The work item's own rules (BR-M3-1, BR-M3-20..25, BR-M3-28), without the database or the endpoint.</summary>
public sealed class WorkItemTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 5, 14, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Created.AddHours(2);

    /// <summary>«Баг» as mod-001 seeds it: «Новая», «Активна», «Разрешена», «Закрыта», «Удалена».</summary>
    private static readonly WorkItemTypeStates Bug = Type("BUG", StateCategory.Proposed, StateCategory.InProgress, StateCategory.Resolved, StateCategory.Completed, StateCategory.Removed);

    /// <summary>«Задача»: «Новая», «Активна», «Закрыта», «Удалена».</summary>
    private static readonly WorkItemTypeStates Task = Type("TASK", StateCategory.Proposed, StateCategory.InProgress, StateCategory.Completed, StateCategory.Removed);

    private static readonly Guid ProjectId = Guid.CreateVersion7();
    private static readonly Guid RootAreaId = Guid.CreateVersion7();

    private static WorkItemTypeStates Type(string key, params StateCategory[] categories) =>
        new(Guid.CreateVersion7(), key, categories.Select((category, index) => new WorkItemTypeState(Guid.CreateVersion7(), index + 1, category)).ToList());

    private static Guid StateOf(WorkItemTypeStates type, StateCategory category) => type.States.First(state => state.Category == category).Id;

    private static WorkItem ABug(int number = 4) =>
        WorkItem.Create(ProjectId, number, Bug, RootAreaId, "Падение при входе", Guid.CreateVersion7(), Created, "creator-sub");

    // US-1 · "A work item is created with only a title" (BR-M3-20, BR-M3-28): the type's initial state, no assignee, an
    // empty description, the root area; changed when created.
    [Fact]
    public void Create_TakesTheInitialStateTheRootAndNothingElse()
    {
        var requestId = Guid.CreateVersion7();

        var item = WorkItem.Create(ProjectId, 4, Bug, RootAreaId, "Падение при входе", requestId, Created, "creator-sub");

        Assert.Equal((ProjectId, 4, Bug.TypeId), (item.ProjectId, item.Number, item.TypeId));
        Assert.Equal(Bug.States[0].Id, item.StateId);
        Assert.Equal(Bug.InitialStateId, item.StateId);
        Assert.Null(item.AssigneeId);
        Assert.Equal(string.Empty, item.Description);
        Assert.Equal(RootAreaId, item.AreaId);
        Assert.Equal(requestId, item.CreationRequestId);
        Assert.Equal(("creator-sub", Created, Created), (item.CreatedBy, item.CreatedAt, item.ChangedAt));
        Assert.NotEqual(Guid.Empty, item.Id);
    }

    // The handler validates and trims the title and takes a number of 1 or more before building: anything else is a defect.
    [Fact]
    public void Create_UncheckedTitleOrNumber_IsADefect()
    {
        Assert.Throws<ArgumentException>(() => WorkItem.Create(ProjectId, 1, Bug, RootAreaId, "  untrimmed ", Guid.CreateVersion7(), Created, "sub"));
        Assert.Throws<ArgumentException>(() => WorkItem.Create(ProjectId, 1, Bug, RootAreaId, "", Guid.CreateVersion7(), Created, "sub"));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkItem.Create(ProjectId, 0, Bug, RootAreaId, "Заголовок", Guid.CreateVersion7(), Created, "sub"));
    }

    // US-3 · "A title and a description are changed in one save" (BR-M3-25, BR-M3-28): both change, ChangedAt moves.
    [Fact]
    public void Save_TitleAndDescription_ChangeTogether_ChangedAtMoves()
    {
        var item = ABug();

        Assert.True(item.Save(new WorkItemEdit(Title: "Падение при входе через SSO", Description: "Шаги"), Bug, Later));

        Assert.Equal(("Падение при входе через SSO", "Шаги", Later), (item.Title, item.Description, item.ChangedAt));
    }

    // US-3 · "Nothing to save" and US-5 · "Choosing the current state changes nothing": equal values change nothing and
    // ChangedAt stays.
    [Fact]
    public void Save_EqualValues_ChangeNothing_ChangedAtStays()
    {
        var item = ABug();

        Assert.False(item.Save(
            new WorkItemEdit(item.Title, item.Description, item.StateId, new AssigneeEdit(null, IsAvailable: true), item.AreaId),
            Bug,
            Later));
        Assert.False(item.Save(new WorkItemEdit(), Bug, Later));

        Assert.Equal(Created, item.ChangedAt);
    }

    // US-5 · "A forward change", "A backward change needs no reason", "A change may skip states" (BR-M3-21): any state of
    // the type but «Удалена», from any other.
    [Fact]
    public void Save_State_MovesForwardBackwardAndSkips()
    {
        var item = ABug();

        Assert.True(item.Save(new WorkItemEdit(StateId: StateOf(Bug, StateCategory.Completed)), Bug, Later));
        Assert.Equal(StateOf(Bug, StateCategory.Completed), item.StateId);

        Assert.True(item.Save(new WorkItemEdit(StateId: StateOf(Bug, StateCategory.Proposed)), Bug, Later));
        Assert.Equal(StateOf(Bug, StateCategory.Proposed), item.StateId);

        Assert.True(item.Save(new WorkItemEdit(StateId: StateOf(Bug, StateCategory.Resolved)), Bug, Later));
        Assert.Equal(StateOf(Bug, StateCategory.Resolved), item.StateId);
    }

    // US-5 · "A state outside the type's own is refused": the type's «Удалена», another type's state, an unknown one.
    [Fact]
    public void Save_StateOfTheRemovedCategoryOrOfAnotherType_IsStateNotAllowed_ItemUnchanged()
    {
        var item = ABug();
        var before = (item.Title, item.StateId, item.ChangedAt);

        foreach (var stateId in new[] { StateOf(Bug, StateCategory.Removed), StateOf(Task, StateCategory.InProgress), Guid.CreateVersion7() })
        {
            // The title is sent too: a refusal changes nothing at all (BR-M3-25 "all of them or none").
            var refusal = Assert.Throws<ConflictException>(() => item.Save(new WorkItemEdit(Title: "Новый заголовок", StateId: stateId), Bug, Later));

            Assert.Equal(WorkItemErrorCodes.StateNotAllowed, refusal.Code);
            Assert.Equal(before, (item.Title, item.StateId, item.ChangedAt));
        }
    }

    // US-5 · "A state change touches no other field".
    [Fact]
    public void Save_StateOnly_LeavesAssigneeAndAreaAsTheyAre()
    {
        var item = ABug();
        var area = Guid.CreateVersion7();
        item.Save(new WorkItemEdit(Assignee: new AssigneeEdit("anna", IsAvailable: true), AreaId: area), Bug, Later);

        item.Save(new WorkItemEdit(StateId: StateOf(Bug, StateCategory.Completed)), Bug, Later.AddHours(1));

        Assert.Equal(("anna", area), (item.AssigneeId, item.AreaId));
    }

    // US-4 · "An assignee is found by name and saved" and "An assignment is cleared" (BR-M3-22).
    [Fact]
    public void Save_Assignee_SetAndCleared()
    {
        var item = ABug();

        Assert.True(item.Save(new WorkItemEdit(Assignee: new AssigneeEdit("anna", IsAvailable: true)), Bug, Later));
        Assert.Equal("anna", item.AssigneeId);

        Assert.True(item.Save(new WorkItemEdit(Assignee: new AssigneeEdit(null, IsAvailable: true)), Bug, Later));
        Assert.Null(item.AssigneeId);
    }

    // US-4 · "An inactive user cannot be newly assigned": refused, nothing changes, the title sent with it included.
    [Fact]
    public void Save_NewlySetUnavailableUser_IsAssigneeUnavailable_ItemUnchanged()
    {
        var item = ABug();

        var refusal = Assert.Throws<ConflictException>(() =>
            item.Save(new WorkItemEdit(Title: "Иное", Assignee: new AssigneeEdit("petr", IsAvailable: false)), Bug, Later));

        Assert.Equal(WorkItemErrorCodes.AssigneeUnavailable, refusal.Code);
        Assert.Equal(("Падение при входе", null, Created), (item.Title, item.AssigneeId, item.ChangedAt));
    }

    // US-4 · "An assignee whose account is disabled later stays": the same, now inactive, assignee sent again with another
    // field is no new assignment, so the save succeeds and keeps them.
    [Fact]
    public void Save_CurrentAssigneeNowUnavailable_IsKept_OtherFieldsSaved()
    {
        var item = ABug();
        item.Save(new WorkItemEdit(Assignee: new AssigneeEdit("anna", IsAvailable: true)), Bug, Later);

        Assert.True(item.Save(new WorkItemEdit(Title: "Иное", Assignee: new AssigneeEdit("anna", IsAvailable: false)), Bug, Later.AddHours(1)));

        Assert.Equal(("Иное", "anna"), (item.Title, item.AssigneeId));
    }

    // The state is checked before the assignee (api-contract § 0.4 step 4).
    [Fact]
    public void Save_StateAndAssigneeBothRefused_StateComesFirst()
    {
        var item = ABug();

        var refusal = Assert.Throws<ConflictException>(() => item.Save(
            new WorkItemEdit(StateId: StateOf(Bug, StateCategory.Removed), Assignee: new AssigneeEdit("petr", IsAvailable: false)),
            Bug,
            Later));

        Assert.Equal(WorkItemErrorCodes.StateNotAllowed, refusal.Code);
    }

    // US-4 · "An area is chosen and saved".
    [Fact]
    public void Save_Area_Moves()
    {
        var item = ABug();
        var api = Guid.CreateVersion7();

        Assert.True(item.Save(new WorkItemEdit(AreaId: api), Bug, Later));

        Assert.Equal(api, item.AreaId);
    }

    // BR-M3-1: the states are always the item's own type's; another type's set is a defect, not a refusal.
    [Fact]
    public void Save_WithAnotherTypesStates_IsADefect()
    {
        Assert.Throws<ArgumentException>(() => ABug().Save(new WorkItemEdit(Title: "Иное"), Task, Later));
    }

    // BR-M3-20, BR-M3-21: the initial state is the first; «Удалена» is never allowed; another type's state never is.
    [Fact]
    public void TypeStates_InitialIsTheFirst_RemovedAndForeignNotAllowed()
    {
        Assert.Equal(Task.States.Single(state => state.Position == 1).Id, Task.InitialStateId);
        Assert.All(Task.States.Where(state => state.Category != StateCategory.Removed), state => Assert.True(Task.Allows(state.Id)));
        Assert.False(Task.Allows(StateOf(Task, StateCategory.Removed)));
        Assert.False(Task.Allows(StateOf(Bug, StateCategory.InProgress)));
    }
}
