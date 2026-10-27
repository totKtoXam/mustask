using Mustask.Domain.Organizations;
using static Mustask.Domain.Tests.Organizations.OrganizationsDomainTestData;

namespace Mustask.Domain.Tests.Organizations;

public sealed class ProjectTests
{
    // feat-028 US-2 · "a project with a name and a key only" (BR-OL-2): no team comes with it; mod-018 BR-AW-11: no area either.
    [Fact]
    public void Create_ComesWithNoTeamAndNoArea()
    {
        var organizationId = Guid.CreateVersion7();

        var created = Project.Create(organizationId, Name(" Пример проекта "), "PRM", Now, Creator);

        Assert.Equal(organizationId, created.OrganizationId);
        Assert.Equal("Пример проекта", created.Name);
        Assert.Equal("PRM", created.Key);
        Assert.Equal(ProjectStatus.Active, created.Status);
        Assert.Equal(Creator, created.CreatedBy);
        Assert.Equal(Now, created.CreatedAt);

        Assert.Null(created.DefaultTeamId);
        Assert.Null(created.Description);
    }

    // feat-028 US-2 · "description and teams in one operation": the description is kept as typed; no description is null.
    [Fact]
    public void Create_WithADescription_KeepsItAsTyped()
    {
        Assert.True(EntityDescription.TryCreate(" О проекте\n", out var description));

        var created = Project.Create(Guid.CreateVersion7(), Name("Проект"), "PRJ", Now, Creator, description);

        Assert.Equal(" О проекте\n", created.Description);
    }

    // US-2 · "edit a project's description", "rename keeps description and links": a rename and a deactivation leave it.
    [Fact]
    public void Describe_SetsAndClears_RenameAndDeactivationKeepIt()
    {
        var created = AProject();
        Assert.True(EntityDescription.TryCreate("Описание", out var description));

        created.Describe(description);
        created.Rename(Name("Новый проект"));
        created.Deactivate();
        created.Restore();

        Assert.Equal("Описание", created.Description);

        created.Describe(null);
        Assert.Null(created.Description);
    }

    // US-3 · "The key is required and well formed": the domain never builds a project with a malformed key.
    [Theory]
    [InlineData("")]
    [InlineData("prm")]
    public void Create_MalformedKey_IsADefect(string key)
    {
        Assert.Throws<ArgumentException>(() => Project.Create(Guid.CreateVersion7(), Name("Проект"), key, Now, Creator));
    }

    // US-3 · "Renaming a project keeps its key" (mod-018 BR-AW-11: a project has no root area to rename).
    [Fact]
    public void Rename_ChangesTheNameOnly_KeepsTheKey()
    {
        var project = AProject();

        project.Rename(Name("Новый проект"));

        Assert.Equal("Новый проект", project.Name);
        Assert.Equal("НОВЫЙ ПРОЕКТ", project.NormalizedName);
        Assert.Equal("PRM", project.Key);
    }

    // mod-017 US-3 · "the administrator chooses the default": a new project names none; naming one, the same one again, and
    // clearing it (BR-OT-4). The setting survives a deactivation and a restore.
    [Fact]
    public void NameDefaultTeam_NamesClearsAndIsANoOpForTheSameValue_SurvivesDeactivation()
    {
        var created = AProject();
        var team = Guid.CreateVersion7();

        Assert.Null(created.DefaultTeamId);
        Assert.False(created.NameDefaultTeam(null));

        Assert.True(created.NameDefaultTeam(team));
        Assert.Equal(team, created.DefaultTeamId);
        Assert.False(created.NameDefaultTeam(team));

        created.Deactivate();
        created.Restore();
        Assert.Equal(team, created.DefaultTeamId);

        Assert.True(created.NameDefaultTeam(null));
        Assert.Null(created.DefaultTeamId);
    }

    // US-3 · "deactivated" and "restored exactly as it was".
    [Fact]
    public void DeactivateThenRestore_TogglesTheStatusOnly()
    {
        var created = AProject();

        created.Deactivate();
        Assert.False(created.IsActive);
        Assert.Equal(ProjectStatus.Deactivated, created.Status);

        created.Restore();
        Assert.True(created.IsActive);
        Assert.Equal("PRM", created.Key);
    }
}
