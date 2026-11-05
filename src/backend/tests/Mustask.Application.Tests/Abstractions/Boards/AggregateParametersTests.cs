using Mustask.Application.Abstractions.Boards;
using Mustask.Domain.Boards;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Abstractions.Boards;

/// <summary>The shared validation of the aggregate reads' query string (feat-048 api-contract section 1).</summary>
public sealed class AggregateParametersTests
{
    private static readonly string Organization = Guid.CreateVersion7().ToString();

    private static AggregateQuery Query(
        string? grouping = null,
        string? group = null,
        string? limit = null,
        string? cursor = null,
        string?[]? projects = null,
        string?[]? teams = null,
        string?[]? departments = null,
        string?[]? search = null) =>
        new(Organization, "stories", grouping, departments, projects, teams, null, null, search, group, null, limit, cursor);

    private static ValidationException Refused(Action action) => Assert.Throws<ValidationException>(action);

    [Fact]
    public void NoGrouping_IsNone_WithTheDefaultLimit()
    {
        var parsed = AggregateParameters.Parse(Query(), AggregateView.Backlogs, groupRead: true);

        Assert.Equal(AggregateGrouping.None, parsed.Grouping);
        Assert.Equal(AggregateParameters.DefaultLimit, parsed.Limit);
        Assert.Null(parsed.After);
    }

    [Fact]
    public void AGroupRead_ParsesTheGroup_AsAnIdOrNone()
    {
        var id = Guid.CreateVersion7();

        var some = AggregateParameters.Parse(Query("team", id.ToString()), AggregateView.Backlogs, groupRead: true);
        var none = AggregateParameters.Parse(Query("team", "none"), AggregateView.Backlogs, groupRead: true);

        Assert.Equal(id, some.Group.Id);
        Assert.Null(none.Group.Id);
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("")]
    public void ABadGroup_IsRefusedOnGroup(string group)
    {
        var error = Refused(() => AggregateParameters.Parse(Query("team", group), AggregateView.Backlogs, groupRead: true));

        Assert.Contains("group", error.Errors.Keys);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("51")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("x")]
    public void ABadLimit_IsRefusedOnLimit(string limit)
    {
        var error = Refused(() => AggregateParameters.Parse(Query(limit: limit), AggregateView.Backlogs, groupRead: true));

        Assert.Contains("limit", error.Errors.Keys);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("50", 50)]
    public void AGoodLimit_IsKept(string limit, int expected) =>
        Assert.Equal(expected, AggregateParameters.Parse(Query(limit: limit), AggregateView.Backlogs, groupRead: true).Limit);

    [Fact]
    public void AGroupingASelectorDoesNotLeaveOpen_IsRefusedOnGrouping()
    {
        var error = Refused(() => AggregateParameters.Parse(
            Query("project", projects: [Guid.CreateVersion7().ToString()]), AggregateView.Backlogs, groupRead: false));

        Assert.Contains("grouping", error.Errors.Keys);
    }

    [Fact]
    public void ASelectorThatNamesNothing_IsNeverAnError()
    {
        var parsed = AggregateParameters.Parse(Query("team", departments: ["not-an-id"]), AggregateView.Backlogs, groupRead: false);

        Assert.True(parsed.DepartmentsSet);
        Assert.Empty(parsed.DepartmentIds);
    }

    [Fact]
    public void TheFingerprint_DiffersWithTheFiltersAndTheGroup_NotWithThePage()
    {
        var one = AggregateParameters.Parse(Query("team", "none", "10"), AggregateView.Backlogs, groupRead: true);
        var samePage = AggregateParameters.Parse(Query("team", "none", "20"), AggregateView.Backlogs, groupRead: true);
        var filtered = AggregateParameters.Parse(Query("team", "none", "10", search: ["a"]), AggregateView.Backlogs, groupRead: true);
        var otherGroup = AggregateParameters.Parse(Query("team", Guid.CreateVersion7().ToString(), "10"), AggregateView.Backlogs, groupRead: true);
        var otherView = AggregateParameters.Parse(Query("team", "none", "10"), AggregateView.Boards, groupRead: true);

        Assert.Equal(one.Fingerprint, samePage.Fingerprint);
        Assert.NotEqual(one.Fingerprint, filtered.Fingerprint);
        Assert.NotEqual(one.Fingerprint, otherGroup.Fingerprint);
        Assert.NotEqual(one.Fingerprint, otherView.Fingerprint);
    }

    [Fact]
    public void ACursor_IsDecodedAgainstTheFingerprint_AndAForeignOneIsRefusedOnCursor()
    {
        var first = AggregateParameters.Parse(Query("team", "none"), AggregateView.Backlogs, groupRead: true);
        var cursor = GroupCursor.Encode(new ChangedPosition(5, 3, "ALP"), first.Fingerprint);

        var next = AggregateParameters.Parse(Query("team", "none", cursor: cursor), AggregateView.Backlogs, groupRead: true);
        var error = Refused(() => AggregateParameters.Parse(Query("department", "none", cursor: cursor), AggregateView.Backlogs, groupRead: true));

        Assert.Equal(new ChangedPosition(5, 3, "ALP"), next.After);
        Assert.Contains("cursor", error.Errors.Keys);
    }

    [Fact]
    public void AByProjectGroup_OfABacklog_TakesARankCursor_ButOfTheWorkItemList_AChangeCursor()
    {
        var project = Guid.CreateVersion7().ToString();
        var backlog = AggregateParameters.Parse(Query("project", project), AggregateView.Backlogs, groupRead: true);
        var list = AggregateParameters.Parse(Query("project", project), AggregateView.WorkItems, groupRead: true);
        var rank = GroupCursor.Encode(new RankPosition(0, 1, 1, 1), backlog.Fingerprint);
        var change = GroupCursor.Encode(new ChangedPosition(1, 1, "ALP"), list.Fingerprint);

        Assert.IsType<RankPosition>(AggregateParameters.Parse(Query("project", project, cursor: rank), AggregateView.Backlogs, groupRead: true).After);
        Assert.IsType<ChangedPosition>(AggregateParameters.Parse(Query("project", project, cursor: change), AggregateView.WorkItems, groupRead: true).After);
    }
}
