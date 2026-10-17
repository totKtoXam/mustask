using Mustask.Domain.Organizations;
using static Mustask.Domain.Tests.Organizations.OrganizationsDomainTestData;

namespace Mustask.Domain.Tests.Organizations;

public sealed class ProjectTests
{
    // US-3 · "A project is created ready to use": its first team and its root, both named after it; the root is owned by
    // the first team and is its default area (BR-M1-9, BR-M1-11, BR-M1-14).
    [Fact]
    public void Create_ComesWithFirstTeamAndRoot_OwnedAndDefault()
    {
        var organizationId = Guid.CreateVersion7();

        var created = Project.Create(organizationId, Name(" Пример проекта "), "PRM", Now, Creator);

        Assert.Equal(organizationId, created.Project.OrganizationId);
        Assert.Equal("Пример проекта", created.Project.Name);
        Assert.Equal("PRM", created.Project.Key);
        Assert.Equal(ProjectStatus.Active, created.Project.Status);
        Assert.Equal(Creator, created.Project.CreatedBy);
        Assert.Equal(Now, created.Project.CreatedAt);

        Assert.Equal(created.Project.Id, created.FirstTeam.ProjectId);
        Assert.Equal("Пример проекта", created.FirstTeam.Name);
        Assert.True(created.FirstTeam.IsFirstTeam);
        Assert.True(created.FirstTeam.IsActive);

        Assert.Equal(created.Project.Id, created.Root.ProjectId);
        Assert.True(created.Root.IsRoot);
        Assert.Equal("Пример проекта", created.Root.Name);
        Assert.Equal(created.FirstTeam.Id, created.Root.OwnerTeamId);
        Assert.Equal(created.FirstTeam.Id, created.Root.DefaultOfTeamId);
    }

    // US-3 · "The key is required and well formed": the domain never builds a project with a malformed key.
    [Theory]
    [InlineData("")]
    [InlineData("prm")]
    public void Create_MalformedKey_IsADefect(string key)
    {
        Assert.Throws<ArgumentException>(() => Project.Create(Guid.CreateVersion7(), Name("Проект"), key, Now, Creator));
    }

    // US-3 · "A rename renames the root and keeps the key" (BR-M1-11).
    [Fact]
    public void Rename_RenamesTheRoot_KeepsTheKey()
    {
        var created = AProject();

        created.Project.Rename(Name("Новый проект"), created.Root);

        Assert.Equal("Новый проект", created.Project.Name);
        Assert.Equal("НОВЫЙ ПРОЕКТ", created.Project.NormalizedName);
        Assert.Equal("Новый проект", created.Root.Name);
        Assert.Equal("НОВЫЙ ПРОЕКТ", created.Root.NormalizedName);
        Assert.Equal("PRM", created.Project.Key);
        Assert.Equal("Пример проекта", created.FirstTeam.Name);
    }

    [Fact]
    public void Rename_WithAnotherProjectsRoot_IsADefect()
    {
        var created = AProject();
        var other = AProject("Другой", "OTH");

        Assert.Throws<ArgumentException>(() => created.Project.Rename(Name("Новый"), other.Root));
    }

    // mod-017 US-3 · "the administrator chooses the default": a new project names none; naming one, the same one again, and
    // clearing it (BR-OT-4). The setting survives a deactivation and a restore.
    [Fact]
    public void NameDefaultTeam_NamesClearsAndIsANoOpForTheSameValue_SurvivesDeactivation()
    {
        var created = AProject();
        var team = Guid.CreateVersion7();

        Assert.Null(created.Project.DefaultTeamId);
        Assert.False(created.Project.NameDefaultTeam(null));

        Assert.True(created.Project.NameDefaultTeam(team));
        Assert.Equal(team, created.Project.DefaultTeamId);
        Assert.False(created.Project.NameDefaultTeam(team));

        created.Project.Deactivate();
        created.Project.Restore();
        Assert.Equal(team, created.Project.DefaultTeamId);

        Assert.True(created.Project.NameDefaultTeam(null));
        Assert.Null(created.Project.DefaultTeamId);
    }

    // US-3 · "deactivated" and "restored exactly as it was".
    [Fact]
    public void DeactivateThenRestore_TogglesTheStatusOnly()
    {
        var created = AProject();

        created.Project.Deactivate();
        Assert.False(created.Project.IsActive);
        Assert.Equal(ProjectStatus.Deactivated, created.Project.Status);

        created.Project.Restore();
        Assert.True(created.Project.IsActive);
        Assert.Equal("PRM", created.Project.Key);
    }
}
