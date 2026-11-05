using Mustask.Domain.Organizations;

namespace Mustask.Domain.Tests.Organizations;

/// <summary>The one definition of the project default team and its mark (mod-017 BR-OT-4).</summary>
public sealed class ProjectDefaultTeamTests
{
    // US-3 · "two teams and none named; one team; named wins".
    [Fact]
    public void Resolve_NamedTeamWins()
    {
        var named = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();

        Assert.Equal(named, ProjectDefaultTeam.Resolve(named, [other, named]));
    }

    [Fact]
    public void Resolve_NoneNamedAndOneTeam_IsTheOnlyTeam()
    {
        var only = Guid.CreateVersion7();

        Assert.Equal(only, ProjectDefaultTeam.Resolve(null, [only]));
        Assert.Equal(only, ProjectDefaultTeam.Resolve(only, [only]));
    }

    [Fact]
    public void Resolve_NoneNamedAndSeveralTeams_IsNone()
    {
        Assert.Null(ProjectDefaultTeam.Resolve(null, [Guid.CreateVersion7(), Guid.CreateVersion7()]));
        Assert.Null(ProjectDefaultTeam.Resolve(null, []));
    }

    // US-3 · "losing the named team": a stale named team that is not active is no longer named.
    [Fact]
    public void Resolve_NamedTeamNotActive_FallsBackToTheOnlyTeamOrNone()
    {
        var gone = Guid.CreateVersion7();
        var only = Guid.CreateVersion7();

        Assert.Equal(only, ProjectDefaultTeam.Resolve(gone, [only]));
        Assert.Null(ProjectDefaultTeam.Resolve(gone, [only, Guid.CreateVersion7()]));
    }

    [Theory]
    [InlineData(true, 1, DefaultTeamMark.Named)]
    [InlineData(true, 3, DefaultTeamMark.Named)]
    [InlineData(false, 1, DefaultTeamMark.OnlyTeam)]
    [InlineData(false, 2, DefaultTeamMark.None)]
    [InlineData(false, 30, DefaultTeamMark.None)]
    public void MarkOf_NamedOrTheOnlyTeam(bool isNamed, int activeTeams, DefaultTeamMark expected)
    {
        Assert.Equal(expected, ProjectDefaultTeam.MarkOf(isNamed, activeTeams));
    }
}
