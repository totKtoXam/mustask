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

        var created = Team.Create(organizationId, Guid.CreateVersion7(), "blue", Name(" Платформа "));

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

        var created = Team.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "blue", Name("MARS"), description);

        Assert.Equal("  Платформа и инструменты\n", created.Description);
    }

    [Fact]
    public void Create_WithoutAnOrganization_IsADefect()
    {
        Assert.Throws<ArgumentException>(() => Team.Create(Guid.Empty, Guid.CreateVersion7(), "blue", Name("MARS")));
    }

    // US-1 · "rename and description": only the team's own name changes.
    [Fact]
    public void Rename_ChangesTheNameOnly()
    {
        var team = Team.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "blue", Name("Платформа"));

        team.Rename(Name("Ядро"));

        Assert.Equal("Ядро", team.Name);
        Assert.Equal("ЯДРО", team.NormalizedName);
        Assert.Null(team.Description);
    }

    // US-1 · "rename and description": a description is set and cleared, never trimmed.
    [Fact]
    public void Describe_SetsAndClears()
    {
        var team = Team.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "blue", Name("Платформа"));
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
        var team = Team.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "blue", Name("Платформа"));

        team.Delete();

        Assert.Equal(TeamStatus.Deleted, team.Status);
        Assert.False(team.IsActive);
        Assert.Throws<InvalidOperationException>(team.Delete);
    }

    // mod-021 US-1 · BR-TD-1: a team stands in a department from the moment it exists, with the colour it was given.
    [Fact]
    public void Create_StandsInTheGivenDepartment_WithTheGivenColour()
    {
        var departmentId = Guid.CreateVersion7();

        var created = Team.Create(Guid.CreateVersion7(), departmentId, "orange", Name("Платформа"));

        Assert.Equal(departmentId, created.DepartmentId);
        Assert.Equal("orange", created.Color);
    }

    [Fact]
    public void Create_WithoutADepartment_IsADefect()
    {
        Assert.Throws<ArgumentException>(() => Team.Create(Guid.CreateVersion7(), Guid.Empty, "blue", Name("Платформа")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Blue")]
    [InlineData("chartreuse")]
    public void Create_WithAColourOutsideThePalette_IsADefect(string color)
    {
        Assert.Throws<ArgumentException>(() => Team.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), color, Name("Платформа")));
    }

    // mod-021 US-3 · BR-TD-4: a move changes the place only; the colour stays.
    [Fact]
    public void MoveTo_ChangesThePlaceAndKeepsTheColour()
    {
        var team = Team.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "green", Name("Платформа"));
        var destination = Guid.CreateVersion7();

        var moved = team.MoveTo(destination);

        Assert.True(moved);
        Assert.Equal(destination, team.DepartmentId);
        Assert.Equal("green", team.Color);
    }

    [Fact]
    public void MoveTo_TheSameDepartment_ChangesNothing()
    {
        var departmentId = Guid.CreateVersion7();
        var team = Team.Create(Guid.CreateVersion7(), departmentId, "green", Name("Платформа"));

        Assert.False(team.MoveTo(departmentId));
        Assert.Equal(departmentId, team.DepartmentId);
    }

    [Fact]
    public void MoveTo_ADeletedTeam_IsADefect()
    {
        var team = Team.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "green", Name("Платформа"));
        team.Delete();

        Assert.Throws<InvalidOperationException>(() => team.MoveTo(Guid.CreateVersion7()));
    }

    // mod-021 BR-TD-5 · a deleted team stands in no department.
    [Fact]
    public void Delete_ClearsTheDepartment_AndKeepsTheColour()
    {
        var team = Team.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "green", Name("Платформа"));

        team.Delete();

        Assert.Null(team.DepartmentId);
        Assert.Equal("green", team.Color);
    }
}
