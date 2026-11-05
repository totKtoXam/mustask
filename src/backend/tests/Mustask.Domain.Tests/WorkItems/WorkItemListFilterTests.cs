using Mustask.Domain.WorkItems;

namespace Mustask.Domain.Tests.WorkItems;

/// <summary>
/// feat-003's three dimensions of the shared filter (BR-WS-2, -3, -8, -10, -11; api-contract § 2.1, § 3): every raw form of the
/// repeated <c>state</c> and <c>assigneeId</c>. A blank value is left out; a value that names nothing keeps its dimension set;
/// one PostgreSQL text cannot hold, or longer than its column, is left out and never an error.
/// </summary>
public sealed class WorkItemListFilterTests
{
    private const string ProjectKey = "MSK";

    private static WorkItemFilter Of(
        IEnumerable<string?>? states = null, IEnumerable<string?>? assignees = null, IEnumerable<string?>? search = null, IEnumerable<string?>? teams = null) =>
        WorkItemFilter.Of(ProjectKey, teams, states, assignees, search);

    // BR-WS-2 · "A filter with no value sets no condition": nothing absent, and only blank values, are no filter at all.
    [Fact]
    public void Of_NothingSet_IsTheNoFilter_ByReference()
    {
        Assert.Same(WorkItemFilter.None, Of());
        Assert.Same(WorkItemFilter.None, Of(states: [], assignees: [], search: []));
        Assert.Same(WorkItemFilter.None, Of(states: ["", "  "], assignees: [null, "\t"], search: ["", " "]));
    }

    // BR-WS-11 · "A blank value is left out ... a parameter with blank values only sets no filter".
    [Fact]
    public void Of_BlankValuesAmongRealOnes_AreLeftOut()
    {
        var filter = Of(states: ["", "Новая", " "], assignees: ["", "user-1", null]);

        Assert.True(filter.StatesSet);
        Assert.Equal(["Новая"], filter.StateNames);
        Assert.True(filter.AssigneesSet);
        Assert.Equal(["user-1"], filter.AssigneeIds);
        Assert.False(filter.NoAssignee);
    }

    // BR-WS-3 · "compared as stored, letter case included": two spellings are two names; the same name twice counts once.
    [Fact]
    public void Of_StateNames_AreKeptAsWritten_EachOnce_InTheOrderGiven()
    {
        var filter = Of(states: ["Разрешена", "разрешена", "Новая", "Разрешена"]);

        Assert.Equal(["Разрешена", "разрешена", "Новая"], filter.StateNames);
    }

    // BR-WS-8 · "«Не назначен» matches the items with no assignee": the literal `none`, in no other letter case.
    [Fact]
    public void Of_AssigneeNone_IsNotAnId_AndOtherCasesAreIds()
    {
        var filter = Of(assignees: ["none", "user-1", "NONE", "user-1", "None"]);

        Assert.True(filter.AssigneesSet);
        Assert.True(filter.NoAssignee);
        Assert.Equal(["user-1", "NONE", "None"], filter.AssigneeIds);
    }

    // BR-WS-10 · "A value that names nothing selects nothing": an id of no shape is kept as written — nothing is validated, so a
    // malformed id, an unknown one and a foreign one are the same to the filter and answer alike.
    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("0199b0a2-0000-7000-8000-000000000001")]
    [InlineData("{0199b0a2-0000-7000-8000-000000000001}")]
    [InlineData("%")]
    [InlineData("'; DROP TABLE work_items; --")]
    public void Of_AnyAssigneeText_IsKeptAsAnId_NothingIsValidated(string value)
    {
        var filter = Of(assignees: [value]);

        Assert.True(filter.AssigneesSet);
        Assert.Equal([value], filter.AssigneeIds);
    }

    // BR-WS-10 / R-6 · "a character no title can hold ... is never an error": PostgreSQL text cannot carry U+0000, so such a value is
    // left out before SQL, and the dimension stays set (it names nothing) — it never turns into a blank, "no filter".
    [Fact]
    public void Of_ValueWithNul_IsLeftOut_ButTheDimensionStaysSet()
    {
        var states = Of(states: ["Нов\0ая"]);
        var assignees = Of(assignees: ["user\01"]);

        Assert.True(states.StatesSet);
        Assert.Empty(states.StateNames);
        Assert.True(assignees.AssigneesSet);
        Assert.Empty(assignees.AssigneeIds);
        Assert.False(assignees.NoAssignee);
    }

    // BR-WS-10 · a value past its column (a state name 64, a user id 255) names nothing and is left out the same way.
    [Fact]
    public void Of_ValueLongerThanItsColumn_IsLeftOut_ButTheDimensionStaysSet()
    {
        var states = Of(states: [new string('я', 65), new string('я', 64)]);
        var assignees = Of(assignees: [new string('u', 256), new string('u', 255)]);

        Assert.Equal([new string('я', 64)], states.StateNames);
        Assert.Equal([new string('u', 255)], assignees.AssigneeIds);
        Assert.True(Of(states: [new string('я', 65)]).StatesSet);
        Assert.Empty(Of(assignees: [new string('u', 256)]).AssigneeIds);
    }

    // The dimensions stand beside the team's and change nothing of it.
    [Fact]
    public void Of_KeepsTheTeamDimension_AsOfTeamsReadsIt()
    {
        var team = Guid.CreateVersion7();

        var filter = Of(states: ["Новая"], teams: [team.ToString(), "none"]);

        Assert.True(filter.TeamsSet);
        Assert.True(filter.NoTeam);
        Assert.Equal([team], filter.TeamIds);
        Assert.True(filter.StatesSet);
        Assert.False(filter.AssigneesSet);
        Assert.Null(filter.Keyword);
    }

    // The team alone is exactly what OfTeams gives: the list, the backlog and the board agree.
    [Fact]
    public void Of_OnlyTheTeam_IsOfTeams()
    {
        var team = Guid.CreateVersion7().ToString();

        Assert.Equal(WorkItemFilter.OfTeams([team, "none"]).TeamIds, Of(teams: [team, "none"]).TeamIds);
        Assert.Same(WorkItemFilter.None, Of(teams: null));
    }

    // US-2 · the options read takes the same names by the same rule.
    [Fact]
    public void OfStates_IsTheStateDimensionAlone()
    {
        var filter = WorkItemFilter.OfStates(["Удалена", "", "Новая", "Удалена"]);

        Assert.True(filter.StatesSet);
        Assert.Equal(["Удалена", "Новая"], filter.StateNames);
        Assert.False(filter.TeamsSet);
        Assert.False(filter.AssigneesSet);
        Assert.Same(WorkItemFilter.None, WorkItemFilter.OfStates(null));
        Assert.Same(WorkItemFilter.None, WorkItemFilter.OfStates([" "]));
    }

    // BR-WS-9 · the keyword rides in the filter: the first non-blank value counts.
    [Fact]
    public void Of_Search_SetsTheKeyword_TheFirstNonBlankCounts()
    {
        var filter = Of(search: ["", "  оплат  ", "второй"]);

        Assert.Equal("оплат", filter.Keyword!.Text);
        Assert.False(filter.TeamsSet);
        Assert.False(filter.StatesSet);
    }
}
