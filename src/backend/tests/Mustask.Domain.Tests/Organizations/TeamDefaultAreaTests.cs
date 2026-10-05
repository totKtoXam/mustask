using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;
using static Mustask.Domain.Tests.Organizations.OrganizationsDomainTestData;

namespace Mustask.Domain.Tests.Organizations;

public sealed class TeamDefaultAreaTests
{
    // US-4 · "The default is among the team's explicitly owned areas".
    [Fact]
    public void RequiresChange_ExplicitlyOwnedArea_IsAChange()
    {
        var project = AProject();
        var platform = ASecondTeam(project);
        var reports = Area.CreateChild(project.Root, Name("Отчёты"), platform.Team);

        Assert.True(TeamDefaultArea.RequiresChange(platform.Team, platform.Area, reports));
    }

    // US-4 · same scenario: an inherited area, or another team's, is refused.
    [Fact]
    public void RequiresChange_InheritedOrOtherTeamsArea_IsRefused()
    {
        var project = AProject();
        var platform = ASecondTeam(project);
        var inherited = Area.CreateChild(platform.Area, Name("API"), ownerTeam: null);
        var others = Area.CreateChild(project.Root, Name("Отчёты"), project.FirstTeam);

        Assert.Equal(
            OrganizationErrorCodes.TeamDefaultAreaNotOwned,
            Assert.Throws<ConflictException>(() => TeamDefaultArea.RequiresChange(platform.Team, platform.Area, inherited)).Code);
        Assert.Equal(
            OrganizationErrorCodes.TeamDefaultAreaNotOwned,
            Assert.Throws<ConflictException>(() => TeamDefaultArea.RequiresChange(platform.Team, platform.Area, others)).Code);
    }

    // US-4 · "The first team's default is fixed" (BR-M1-9).
    [Fact]
    public void RequiresChange_FirstTeam_IsRefused()
    {
        var project = AProject();
        var owned = Area.CreateChild(project.Root, Name("Отчёты"), project.FirstTeam);

        var refusal = Assert.Throws<ConflictException>(() => TeamDefaultArea.RequiresChange(project.FirstTeam, project.Root, owned));

        Assert.Equal(OrganizationErrorCodes.TeamFirstTeamDefaultFixed, refusal.Code);
    }

    // api-contract § 3.5: setting the current default again changes nothing.
    [Fact]
    public void RequiresChange_CurrentDefaultAgain_IsANoOp()
    {
        var project = AProject();
        var platform = ASecondTeam(project);

        Assert.False(TeamDefaultArea.RequiresChange(platform.Team, platform.Area, platform.Area));
    }

    [Fact]
    public void RequiresChange_AreaOfAnotherProjectOrWrongCurrent_IsADefect()
    {
        var project = AProject();
        var other = AProject("Другой", "OTH");
        var platform = ASecondTeam(project);

        Assert.Throws<ArgumentException>(() => TeamDefaultArea.RequiresChange(platform.Team, platform.Area, other.Root));
        Assert.Throws<ArgumentException>(() => TeamDefaultArea.RequiresChange(platform.Team, project.Root, platform.Area));
    }
}
