using Mustask.Domain.Boards;
using Mustask.Domain.Process;

namespace Mustask.Domain.Tests.Boards;

/// <summary>The level segment and its mapping onto a process's levels (spec § Model; api-contract § 0.2).</summary>
public sealed class BoardLevelTests
{
    // US-1 · "The level is chosen by tabs and kept in the address": three segments; `tasks` and any other spelling are none.
    [Theory]
    [InlineData("epics", BoardLevel.Epics)]
    [InlineData("features", BoardLevel.Features)]
    [InlineData("stories", BoardLevel.Stories)]
    public void TryParse_TheThreeSegments_RoundTrip(string segment, BoardLevel level)
    {
        Assert.True(BoardLevelNames.TryParse(segment, out var parsed));
        Assert.Equal(level, parsed);
        Assert.Equal(segment, parsed.ToWireName());
    }

    [Theory]
    [InlineData("tasks")]
    [InlineData("Stories")]
    [InlineData("story")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_AnythingElse_IsNone(string? segment)
    {
        Assert.False(BoardLevelNames.TryParse(segment, out _));
    }

    // The seeded process (mod-001): «Эпики» rank 4, «Фичи» 3 (portfolio), «Истории» 2 (requirement), «Задачи» 1 (task).
    [Fact]
    public void Resolve_SeededProcess_MapsByKindAndRank()
    {
        var levels = SeededProcess.Blueprint.Levels.Select(level => (Level: level, Process: new ProcessLevel(Guid.NewGuid(), level.Rank, level.Kind))).ToList();
        string NameOf(ProcessLevel? resolved) => levels.Single(pair => pair.Process == resolved).Level.Name;

        var all = levels.Select(pair => pair.Process).ToList();

        Assert.Equal("Эпики", NameOf(BoardLevel.Epics.Resolve(all)));
        Assert.Equal("Фичи", NameOf(BoardLevel.Features.Resolve(all)));
        Assert.Equal("Истории", NameOf(BoardLevel.Stories.Resolve(all)));
    }

    // A process with one portfolio level has `features` and no `epics`.
    [Fact]
    public void Resolve_OnePortfolioLevel_HasNoEpics()
    {
        var portfolio = new ProcessLevel(Guid.NewGuid(), 3, BacklogLevelKind.Portfolio);
        var requirement = new ProcessLevel(Guid.NewGuid(), 2, BacklogLevelKind.Requirement);

        Assert.Null(BoardLevel.Epics.Resolve([portfolio, requirement]));
        Assert.Equal(portfolio, BoardLevel.Features.Resolve([portfolio, requirement]));
        Assert.Equal(requirement, BoardLevel.Stories.Resolve([portfolio, requirement]));
    }
}
