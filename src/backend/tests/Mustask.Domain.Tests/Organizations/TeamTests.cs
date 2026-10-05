using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;
using static Mustask.Domain.Tests.Organizations.OrganizationsDomainTestData;

namespace Mustask.Domain.Tests.Organizations;

public sealed class TeamTests
{
    // US-4 · "A second team comes with its own area": a child of the root named after it, owned by it and its default.
    [Fact]
    public void CreateWithArea_AddsARootChildOwnedAndDefault()
    {
        var project = AProject();

        var created = Team.CreateWithArea(project.Project.Id, Name(" Платформа "), project.Root);

        Assert.Equal("Платформа", created.Team.Name);
        Assert.False(created.Team.IsFirstTeam);
        Assert.True(created.Team.IsActive);
        Assert.Equal(project.Root.Id, created.Area.ParentId);
        Assert.Equal("Платформа", created.Area.Name);
        Assert.Equal(created.Team.Id, created.Area.OwnerTeamId);
        Assert.Equal(created.Team.Id, created.Area.DefaultOfTeamId);
    }

    [Fact]
    public void CreateWithArea_UnderAChildOrAnotherProjectsRoot_IsADefect()
    {
        var project = AProject();
        var other = AProject("Другой", "OTH");
        var child = Area.CreateChild(project.Root, Name("Отчёты"), ownerTeam: null);

        Assert.Throws<ArgumentException>(() => Team.CreateWithArea(project.Project.Id, Name("Т"), child));
        Assert.Throws<ArgumentException>(() => Team.CreateWithArea(project.Project.Id, Name("Т"), other.Root));
    }

    // US-4 · "A team is renamed": only the team's own name changes.
    [Fact]
    public void Rename_ChangesTheTeamOnly()
    {
        var project = AProject();
        var second = ASecondTeam(project);

        second.Team.Rename(Name("Ядро"));

        Assert.Equal("Ядро", second.Team.Name);
        Assert.Equal("ЯДРО", second.Team.NormalizedName);
        Assert.Equal("Платформа", second.Area.Name);
    }

    // US-7 · "A team is deleted": the status is terminal.
    [Fact]
    public void Delete_SecondTeam_IsDeleted()
    {
        var second = ASecondTeam(AProject());

        second.Team.Delete();

        Assert.Equal(TeamStatus.Deleted, second.Team.Status);
        Assert.False(second.Team.IsActive);
        Assert.Throws<InvalidOperationException>(second.Team.Delete);
    }

    // US-7 · "The first team cannot be deleted" (BR-M1-18).
    [Fact]
    public void Delete_FirstTeam_IsRefused()
    {
        var project = AProject();

        var refusal = Assert.Throws<ConflictException>(project.FirstTeam.Delete);

        Assert.Equal(OrganizationErrorCodes.TeamFirstTeamCannotBeDeleted, refusal.Code);
        Assert.True(project.FirstTeam.IsActive);
    }
}
