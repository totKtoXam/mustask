using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;
using static Mustask.Domain.Tests.Organizations.OrganizationsDomainTestData;

namespace Mustask.Domain.Tests.Organizations;

public sealed class TeamTests
{
    // mod-017 US-4 · "A team added later is an ordinary one" (BR-OT-11): a name and a status, no area, no first-team role.
    [Fact]
    public void Create_IsAnActiveOrdinaryTeamOfTheProject()
    {
        var project = AProject();

        var created = Team.Create(project.Project.Id, Name(" Платформа "));

        Assert.Equal(project.Project.Id, created.ProjectId);
        Assert.Equal("Платформа", created.Name);
        Assert.Equal("ПЛАТФОРМА", created.NormalizedName);
        Assert.True(created.IsActive);
    }

    // US-4 · "A team is renamed": only the team's own name changes.
    [Fact]
    public void Rename_ChangesTheTeamOnly()
    {
        var project = AProject();
        var second = ASecondTeam(project);

        second.Rename(Name("Ядро"));

        Assert.Equal("Ядро", second.Name);
        Assert.Equal("ЯДРО", second.NormalizedName);
        Assert.Equal(project.Project.Id, second.ProjectId);
    }

    // US-7 · "A team is deleted": the status is terminal.
    [Fact]
    public void Delete_SecondTeam_IsDeleted()
    {
        var second = ASecondTeam(AProject());

        second.Delete(activeTeamsOfProject: 2);

        Assert.Equal(TeamStatus.Deleted, second.Status);
        Assert.False(second.IsActive);
        Assert.Throws<InvalidOperationException>(() => second.Delete(activeTeamsOfProject: 2));
    }

    // mod-017 US-7 · "any team renamed or deleted" (BR-OT-11): no team is the first; the team a project came with goes like
    // any other while another remains.
    [Fact]
    public void Delete_TheTeamTheProjectCameWith_IsAllowedWhenAnotherRemains()
    {
        var project = AProject();

        project.Team.Delete(activeTeamsOfProject: 2);

        Assert.Equal(TeamStatus.Deleted, project.Team.Status);
    }

    // mod-017 US-7 · "the last team cannot be deleted" (BR-OT-11, the interim guard lifted by feat-028): refused, and the team
    // stays active.
    [Fact]
    public void Delete_LastTeam_IsRefused()
    {
        var project = AProject();

        var refusal = Assert.Throws<ConflictException>(() => project.Team.Delete(activeTeamsOfProject: 1));

        Assert.Equal(OrganizationErrorCodes.TeamLastTeamCannotBeDeleted, refusal.Code);
        Assert.True(project.Team.IsActive);
    }
}
