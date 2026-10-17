using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;

namespace Mustask.Domain.Tests.WorkItems;

/// <summary>The item's owning team (mod-017 BR-OT-1..-3): set, cleared, an equal value a no-op, a created item with its team.</summary>
public sealed class WorkItemOwningTeamTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Created.AddHours(3);

    private static readonly Guid ProjectId = Guid.CreateVersion7();
    private static readonly Guid RootAreaId = Guid.CreateVersion7();

    private static readonly WorkItemTypeStates Story = new(
        Guid.CreateVersion7(),
        "US",
        AppearanceToken.Story,
        [
            new WorkItemTypeState(Guid.CreateVersion7(), 1, StateCategory.Proposed),
            new WorkItemTypeState(Guid.CreateVersion7(), 2, StateCategory.InProgress),
            new WorkItemTypeState(Guid.CreateVersion7(), 3, StateCategory.Completed),
            new WorkItemTypeState(Guid.CreateVersion7(), 4, StateCategory.Removed),
        ]);

    private static WorkItem AStory(Guid? team = null) =>
        WorkItem.Create(ProjectId, 12, Story, RootAreaId, "Вход по ссылке", Guid.CreateVersion7(), Created, "creator-sub", team);

    // US-2 · "the dialog creates with the team it shows": a created item has the team it is given, or none.
    [Fact]
    public void Create_WithATeam_IsOwnedByIt_WithoutOneByNone()
    {
        var team = Guid.CreateVersion7();

        Assert.Equal(team, AStory(team).OwningTeamId);
        Assert.Null(AStory().OwningTeamId);
    }

    // US-1 · "changing the team changes nothing else on the item" (BR-OT-1).
    [Fact]
    public void Save_SetTeam_ChangesTheTeamAndChangedAtOnly()
    {
        var item = AStory();
        var team = Guid.CreateVersion7();

        var changed = item.Save(new WorkItemEdit(Team: new TeamEdit(team)), Story, Later);

        Assert.True(changed);
        Assert.Equal(team, item.OwningTeamId);
        Assert.Equal(Later, item.ChangedAt);
        Assert.Equal((ProjectId, 12, RootAreaId, "Вход по ссылке"), (item.ProjectId, item.Number, item.AreaId, item.Title));
        Assert.Equal(Story.InitialStateId, item.StateId);
        Assert.Null(item.AssigneeId);
        Assert.Null(item.ParentId);
    }

    // US-1 · "none is a choice": «Без команды» is a value, not an absence.
    [Fact]
    public void Save_NoTeam_ClearsTheTeam()
    {
        var item = AStory(Guid.CreateVersion7());

        var changed = item.Save(new WorkItemEdit(Team: new TeamEdit(null)), Story, Later);

        Assert.True(changed);
        Assert.Null(item.OwningTeamId);
        Assert.Equal(Later, item.ChangedAt);
    }

    // US-1 · "none again is a no-op" · "an equal value changes nothing": «Изменён» moves only when the value differs.
    [Fact]
    public void Save_TheSameTeam_OrNoneAgain_IsANoOp_KeepsChangedAt()
    {
        var team = Guid.CreateVersion7();
        var owned = AStory(team);
        var unowned = AStory();

        Assert.False(owned.Save(new WorkItemEdit(Team: new TeamEdit(team)), Story, Later));
        Assert.False(unowned.Save(new WorkItemEdit(Team: new TeamEdit(null)), Story, Later));

        Assert.Equal(team, owned.OwningTeamId);
        Assert.Equal(Created, owned.ChangedAt);
        Assert.Equal(Created, unowned.ChangedAt);
    }

    // US-1 · "not sent keeps": a save that sends no team leaves the stored value, and moves nothing by itself.
    [Fact]
    public void Save_TeamNotSent_KeepsTheStoredTeam()
    {
        var team = Guid.CreateVersion7();
        var item = AStory(team);

        var changed = item.Save(new WorkItemEdit(Title: "Вход по ссылке из письма"), Story, Later);

        Assert.True(changed);
        Assert.Equal(team, item.OwningTeamId);
    }

    // US-1 · "a team and other fields in one save": all or nothing, so a refused state leaves the team as it was.
    [Fact]
    public void Save_RefusedState_LeavesTheTeamAsItWas()
    {
        var team = Guid.CreateVersion7();
        var item = AStory(team);

        Assert.ThrowsAny<Exception>(() => item.Save(new WorkItemEdit(StateId: Guid.CreateVersion7(), Team: new TeamEdit(null)), Story, Later));

        Assert.Equal(team, item.OwningTeamId);
        Assert.Equal(Created, item.ChangedAt);
    }
}
