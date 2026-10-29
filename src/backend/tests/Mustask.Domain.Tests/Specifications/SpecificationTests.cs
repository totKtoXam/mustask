using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;

namespace Mustask.Domain.Tests.Specifications;

/// <summary>
/// mod-018 (ADR-0031 rule 9): the business rules that lived in Infrastructure as EF predicates are domain specifications, so each
/// decision is checked here without a database. The generated SQL is pinned elsewhere (Infrastructure.Tests, statement counts).
/// </summary>
public sealed class SpecificationTests
{
    [Theory]
    [InlineData(OrganizationStatus.Active, ProjectStatus.Active, true)]
    [InlineData(OrganizationStatus.Active, ProjectStatus.Deactivated, false)]
    [InlineData(OrganizationStatus.Deactivated, ProjectStatus.Active, false)]
    [InlineData(OrganizationStatus.Deactivated, ProjectStatus.Deactivated, false)]
    public void ProjectIsReachable_OnlyWhenItAndItsOrganizationAreActive(OrganizationStatus organization, ProjectStatus project, bool expected) =>
        Assert.Equal(expected, ReachabilityRules.IsReachable(organization, project));

    [Theory]
    [InlineData(OrganizationStatus.Active, TeamStatus.Active, true)]
    [InlineData(OrganizationStatus.Active, TeamStatus.Deleted, false)]
    [InlineData(OrganizationStatus.Deactivated, TeamStatus.Active, false)]
    public void TeamIsReachable_OnlyWhenItAndItsOrganizationAreActive(OrganizationStatus organization, TeamStatus team, bool expected) =>
        Assert.Equal(expected, ReachabilityRules.IsReachable(organization, team));

    [Fact]
    public void ActiveTeamOf_SelectsTheOrganizationsActiveTeamsOnly()
    {
        var organization = Guid.CreateVersion7();
        Assert.True(EntityName.TryCreate("Марс", out var name));
        var active = Team.Create(organization, name);
        var deleted = Team.Create(organization, name);
        deleted.Delete();
        var foreign = Team.Create(Guid.CreateVersion7(), name);
        var rule = ReachabilityRules.ActiveTeamOf(organization).Compile();

        Assert.Equal([true, false, false], [rule(active), rule(deleted), rule(foreign)]);
        Assert.True(ReachabilityRules.ActiveTeam.Compile()(foreign));
    }

    [Fact]
    public void ActiveOrganization_IsTheActiveStatus()
    {
        var rule = ReachabilityRules.ActiveOrganization.Compile();

        Assert.True(rule(Organization.Create(Guid.CreateVersion7(), EntityNameOf("Орг"), ProcessId.New(), DateTimeOffset.UnixEpoch, "creator-sub")));
    }

    private static EntityName EntityNameOf(string raw)
    {
        Assert.True(EntityName.TryCreate(raw, out var name));

        return name;
    }

    [Fact]
    public void LiveAndDeleted_SplitItemsByTheRemovedCategory()
    {
        var proposed = new WorkItemTypeState(Guid.CreateVersion7(), 1, StateCategory.Proposed);
        var removed = new WorkItemTypeState(Guid.CreateVersion7(), 2, StateCategory.Removed);
        var type = new WorkItemTypeStates(Guid.CreateVersion7(), "STORY", AppearanceToken.Story, [proposed, removed]);
        var live = Item(type);
        var gone = Item(type);
        gone.Delete(type, "someone", DateTimeOffset.UnixEpoch);
        var removedIds = new[] { removed.Id }.AsQueryable();

        Assert.Equal([true, false], [WorkItemSetRules.Live(removedIds).Compile()(live), WorkItemSetRules.Live(removedIds).Compile()(gone)]);
        Assert.Equal([false, true], [WorkItemSetRules.Deleted(removedIds).Compile()(live), WorkItemSetRules.Deleted(removedIds).Compile()(gone)]);
        Assert.True(WorkItemSetRules.IsRemoved(StateCategory.Removed));
        Assert.False(WorkItemSetRules.IsRemoved(StateCategory.Completed));
        Assert.True(type.IsRemoved(removed.Id));
    }

    [Fact]
    public void OwnedItems_AreTheTeamsItemsInTheNamedProjects_InEveryState()
    {
        var removed = new WorkItemTypeState(Guid.CreateVersion7(), 2, StateCategory.Removed);
        var type = new WorkItemTypeStates(Guid.CreateVersion7(), "STORY", AppearanceToken.Story, [new WorkItemTypeState(Guid.CreateVersion7(), 1, StateCategory.Proposed), removed]);
        var project = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        var team = Guid.CreateVersion7();
        var owned = Item(type, project, team);
        var deletedOwned = Item(type, project, team);
        deletedOwned.Delete(type, "someone", DateTimeOffset.UnixEpoch);
        var ofAnotherTeam = Item(type, project, Guid.CreateVersion7());
        var elsewhere = Item(type, other, team);
        var rule = OwnedItemsRule.Of([project, project], team);
        var matches = rule.Matches.Compile();

        Assert.Equal([project], rule.ProjectIds);
        Assert.Equal([true, true, false, false], [matches(owned), matches(deletedOwned), matches(ofAnotherTeam), matches(elsewhere)]);
        Assert.True(OwnedItemsRule.Of([], team).IsEmpty);
    }

    private static WorkItem Item(WorkItemTypeStates type, Guid? project = null, Guid? team = null) =>
        WorkItem.Create(project ?? Guid.CreateVersion7(), 1, type, Guid.CreateVersion7(), "Вход по ссылке", Guid.CreateVersion7(), DateTimeOffset.UnixEpoch, "creator-sub", team);
}
