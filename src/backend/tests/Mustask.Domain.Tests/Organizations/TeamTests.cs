using Mustask.Domain.Organizations;
using static Mustask.Domain.Tests.Organizations.OrganizationsDomainTestData;

namespace Mustask.Domain.Tests.Organizations;

public sealed class TeamTests
{
    // feat-028 US-1 · "created with a name only" (BR-OL-2): a team belongs to the organization, has a name and a status, and no
    // description, project, area or first-team role.
    [Fact]
    public void Create_IsAnActiveTeamOfTheOrganization_WithNoDescription()
    {
        var organizationId = Guid.CreateVersion7();

        var created = Team.Create(organizationId, Name(" Платформа "));

        Assert.Equal(organizationId, created.OrganizationId);
        Assert.Equal("Платформа", created.Name);
        Assert.Equal("ПЛАТФОРМА", created.NormalizedName);
        Assert.Null(created.Description);
        Assert.True(created.IsActive);
    }

    // US-1 · "created with its optional parts": the description is kept as typed.
    [Fact]
    public void Create_WithADescription_KeepsItAsTyped()
    {
        Assert.True(EntityDescription.TryCreate("  Платформа и инструменты\n", out var description));

        var created = Team.Create(Guid.CreateVersion7(), Name("MARS"), description);

        Assert.Equal("  Платформа и инструменты\n", created.Description);
    }

    [Fact]
    public void Create_WithoutAnOrganization_IsADefect()
    {
        Assert.Throws<ArgumentException>(() => Team.Create(Guid.Empty, Name("MARS")));
    }

    // US-1 · "rename and description": only the team's own name changes.
    [Fact]
    public void Rename_ChangesTheNameOnly()
    {
        var team = Team.Create(Guid.CreateVersion7(), Name("Платформа"));

        team.Rename(Name("Ядро"));

        Assert.Equal("Ядро", team.Name);
        Assert.Equal("ЯДРО", team.NormalizedName);
        Assert.Null(team.Description);
    }

    // US-1 · "rename and description": a description is set and cleared, never trimmed.
    [Fact]
    public void Describe_SetsAndClears()
    {
        var team = Team.Create(Guid.CreateVersion7(), Name("Платформа"));
        Assert.True(EntityDescription.TryCreate(" текст ", out var description));

        team.Describe(description);
        Assert.Equal(" текст ", team.Description);

        team.Describe(null);
        Assert.Null(team.Description);
    }

    // US-5 · "a project's last team can be deleted" (BR-OL-10): the status is terminal and there is no guard.
    [Fact]
    public void Delete_IsTerminal_AndHasNoGuard()
    {
        var team = Team.Create(Guid.CreateVersion7(), Name("Платформа"));

        team.Delete();

        Assert.Equal(TeamStatus.Deleted, team.Status);
        Assert.False(team.IsActive);
        Assert.Throws<InvalidOperationException>(team.Delete);
    }
}
