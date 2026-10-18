using Mustask.Domain.WorkItems;

namespace Mustask.Domain.Tests.WorkItems;

/// <summary>The team filter's one meaning (mod-017 BR-OT-8, api-contract § 3), every raw form.</summary>
public sealed class WorkItemFilterTests
{
    // US-5 · "the choice lives with the visit": absent or empty is no filter.
    [Fact]
    public void OfTeams_AbsentOrEmpty_IsNoFilter()
    {
        Assert.Equal(WorkItemFilter.None, WorkItemFilter.OfTeams(null));
        Assert.Equal(WorkItemFilter.None, WorkItemFilter.OfTeams([]));
        Assert.False(WorkItemFilter.None.TeamsSet);
    }

    // US-5 · "filtered by one, several, none".
    [Fact]
    public void OfTeams_IdsAndNone_AreTheValues_AnyCase()
    {
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();

        var filter = WorkItemFilter.OfTeams([first.ToString(), second.ToString().ToUpperInvariant(), "none"]);

        Assert.True(filter.TeamsSet);
        Assert.True(filter.NoTeam);
        Assert.Equal(2, filter.TeamIds.Count);
        Assert.Contains(first, filter.TeamIds);
        Assert.Contains(second, filter.TeamIds);
    }

    [Fact]
    public void OfTeams_OnlyNone_IsSetWithNoTeamIds()
    {
        var filter = WorkItemFilter.OfTeams(["none"]);

        Assert.True(filter.TeamsSet);
        Assert.True(filter.NoTeam);
        Assert.Empty(filter.TeamIds);
    }

    [Fact]
    public void OfTeams_RepeatedValuesCountOnce()
    {
        var id = Guid.CreateVersion7();

        var filter = WorkItemFilter.OfTeams([id.ToString(), id.ToString(), "none", "none"]);

        Assert.Single(filter.TeamIds);
    }

    // US-5 · "a value that is not a team of the project": a malformed value is ignored but the filter is set, so it selects nothing.
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("abc")]
    [InlineData("NONE")]
    [InlineData("None")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("0199b0a2")]
    [InlineData("{0199b0a2-0000-7000-8000-000000000001}")]
    public void OfTeams_MalformedValue_IsIgnored_ButTheFilterIsSet(string value)
    {
        var filter = WorkItemFilter.OfTeams([value]);

        Assert.True(filter.TeamsSet);
        Assert.False(filter.NoTeam);
        Assert.Empty(filter.TeamIds);
    }

    [Fact]
    public void OfTeams_NullValue_IsIgnored_ButTheFilterIsSet()
    {
        var filter = WorkItemFilter.OfTeams([null]);

        Assert.True(filter.TeamsSet);
        Assert.Empty(filter.TeamIds);
        Assert.False(filter.NoTeam);
    }

    // feat-040 US-2 · "no value is no filter": absent or empty, the project dimension is off and the team one is untouched.
    [Fact]
    public void OfProjects_AbsentOrEmpty_IsNoFilter()
    {
        Assert.Equal(WorkItemFilter.None, WorkItemFilter.OfProjects(null));
        Assert.Equal(WorkItemFilter.None, WorkItemFilter.OfProjects([]));
        Assert.False(WorkItemFilter.None.ProjectsSet);
        Assert.Empty(WorkItemFilter.None.ProjectIds);
    }

    // feat-040 US-2 · "one, several values": each a canonical project id in either letter case; a repeat counts once.
    [Fact]
    public void OfProjects_Ids_AreTheValues_AnyCase_RepeatsCountOnce()
    {
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();

        var filter = WorkItemFilter.OfProjects([first.ToString(), second.ToString().ToUpperInvariant(), first.ToString()]);

        Assert.True(filter.ProjectsSet);
        Assert.Equal(2, filter.ProjectIds.Count);
        Assert.Contains(first, filter.ProjectIds);
        Assert.Contains(second, filter.ProjectIds);
        Assert.False(filter.TeamsSet);
    }

    // feat-040 BR-TS-8 · "a value that names no such project selects nothing": a malformed value is ignored but the filter is
    // set, so an item can match none of its (zero) values. «none» is the team dimension's literal, not a project.
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("abc")]
    [InlineData("none")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("0199b0a2")]
    [InlineData("{0199b0a2-0000-7000-8000-000000000001}")]
    public void OfProjects_MalformedValue_IsIgnored_ButTheFilterIsSet(string value)
    {
        var filter = WorkItemFilter.OfProjects([value]);

        Assert.True(filter.ProjectsSet);
        Assert.Empty(filter.ProjectIds);
    }

    // feat-040 BR-TS-8 · "beside a valid value an invalid one adds nothing": Beta and an unknown id show Beta's items.
    [Fact]
    public void OfProjects_AValidValueBesideAnInvalidOne_KeepsOnlyTheValid()
    {
        var beta = Guid.CreateVersion7();

        var filter = WorkItemFilter.OfProjects(["not-an-id", beta.ToString(), null]);

        Assert.True(filter.ProjectsSet);
        Assert.Equal([beta], filter.ProjectIds);
    }
}
