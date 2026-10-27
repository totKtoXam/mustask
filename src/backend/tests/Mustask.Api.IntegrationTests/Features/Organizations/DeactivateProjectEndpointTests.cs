using System.Net;
using System.Text.Json;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/deactivate</c>
/// (api-contract § 2.6) through the real host and a real PostgreSQL: US-3 "A
/// project is deactivated" (plan-backend § 13, lane L-13). Every test builds its
/// own organization (<see cref="OrganizationsTestData"/>): the database is shared
/// and never emptied.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DeactivateProjectEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> DeactivateAsync(Guid organizationId, string projectId, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            $"/api/organizations/{organizationId}/projects/{Uri.EscapeDataString(projectId)}/deactivate",
            token: token ?? OrganizationsTokens.SignedIn());

    private Task<Answer> DeactivateAsync(OrganizationStructure structure) =>
        DeactivateAsync(structure.OrganizationId, structure.ProjectId.ToString());

    private Task<Answer> GetAsync(string path) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, path, token: OrganizationsTokens.SignedIn());

    // US-3 · "A project is deactivated": no longer among the active projects, among the deactivated ones (BR-M1-24). Another
    // project of the organization stays where it was.
    [Fact]
    public async Task Deactivate_Returns204_LeavesActiveList_JoinsDeactivated()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var second = await OrganizationsTestData.CreateProjectAsync(factory, structure.OrganizationId, "Второй проект", "SEC");
        var projects = $"/api/organizations/{structure.OrganizationId}/projects";

        var answer = await DeactivateAsync(structure);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal(JsonValueKind.Undefined, answer.Body.ValueKind);

        var active = await GetAsync(projects);
        var deactivated = await GetAsync($"{projects}/deactivated");

        Assert.Equal(HttpStatusCode.OK, active.Status);
        Assert.Equal([second.Project.Id], Ids(active.Body));
        Assert.Equal(HttpStatusCode.OK, deactivated.Status);
        var listed = Assert.Single(deactivated.Body.EnumerateArray());
        Assert.Equal(structure.ProjectId, listed.GetProperty("id").GetGuid());
        Assert.Equal("Пример проекта", listed.GetProperty("name").GetString());
        Assert.Equal("PRM", listed.GetProperty("key").GetString());
    }

    // US-1 · "A visitor who is not signed in sees nothing".
    [Fact]
    public async Task Deactivate_NoToken_Returns401_ChangesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            $"{OrganizationsApi.Project(structure.OrganizationId, structure.ProjectId)}/deactivate");

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal(ProjectStatus.Active, (await ReadProjectAsync(structure.ProjectId)).Status);
    }

    // US-3 · "A project identifier that names no project of the organization" (BR-M1-20, BR-M1-22): malformed, unknown, or of
    // another organization. The project of the other organization stays active, and so does the addressed organization's own.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    public async Task Deactivate_ProjectNotOfOrganization_Returns404ProjectNotFound_ChangesNothing(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var projectId = how switch
        {
            "malformed" => "PRM",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => other.ProjectId.ToString(),
        };

        var answer = await DeactivateAsync(structure.OrganizationId, projectId);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal("Проект не найден.", answer.Detail);
        Assert.Equal(ProjectStatus.Active, (await ReadProjectAsync(structure.ProjectId)).Status);
        Assert.Equal(ProjectStatus.Active, (await ReadProjectAsync(other.ProjectId)).Status);
    }

    // US-3 · "A project is deactivated" (api-contract § 2.6, "already deactivated included"): a deactivated project takes no
    // change, so deactivating it answers project.not_found, whatever sent it there, and it stays deactivated.
    [Fact]
    public async Task Deactivate_UnderDeactivatedProject_Returns404AndChangesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);

        var answer = await DeactivateAsync(structure);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal("Проект не найден.", answer.Detail);
        Assert.Equal(ProjectStatus.Deactivated, (await ReadProjectAsync(structure.ProjectId)).Status);
    }

    // The same, one request after the other: the first deactivation answers 204, the second finds nothing to deactivate.
    [Fact]
    public async Task Deactivate_Twice_FirstReturns204_SecondReturns404ProjectNotFound()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var first = await DeactivateAsync(structure);
        var second = await DeactivateAsync(structure);

        Assert.Equal(HttpStatusCode.NoContent, first.Status);
        Assert.Equal(HttpStatusCode.NotFound, second.Status);
        Assert.Equal("organizations.project.not_found", second.ErrorCode);
        Assert.Equal(ProjectStatus.Deactivated, (await ReadProjectAsync(structure.ProjectId)).Status);
    }

    // US-3 · "A project is deactivated": opening its address, or the address of its linked teams or areas, answers «Проект не
    // найден» (BR-M1-20, ADR-0026 rule 5). Each read answers before the deactivation (so it is the deactivation that changes
    // it), and project.not_found after.
    [Theory]
    [InlineData("project")]
    [InlineData("teams")]
    [InlineData("areas")]
    public async Task Deactivate_ThenReadsBeneath_Return404ProjectNotFound(string read)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var address = read switch
        {
            "project" => OrganizationsApi.Project(structure.OrganizationId, structure.ProjectId),
            "teams" => OrganizationsApi.Teams(structure.OrganizationId, structure.ProjectId),
            _ => OrganizationsApi.ProjectAreas(structure.OrganizationId, structure.ProjectId),
        };

        var before = await GetAsync(address);
        var deactivation = await DeactivateAsync(structure);
        var after = await GetAsync(address);

        Assert.Equal(HttpStatusCode.OK, before.Status);
        Assert.Equal(HttpStatusCode.NoContent, deactivation.Status);
        Assert.Equal(HttpStatusCode.NotFound, after.Status);
        Assert.Equal("organizations.project.not_found", after.ErrorCode);
        Assert.Equal("Проект не найден.", after.Detail);
    }

    // US-3 · "nothing under it is deleted" (ADR-0026 rule 2): the deactivation writes the project's own row. Every team and
    // every area is where it was, and the default-team setting is the one the administrator named; the project keeps its name and key; the organization and
    // the project's sibling stay active.
    [Fact]
    public async Task Deactivate_DeletesNothing_ChangesOnlyTheProjectsOwnStatus()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var sibling = await OrganizationsTestData.CreateProjectAsync(factory, structure.OrganizationId, "Второй проект", "SEC");
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.OrganizationId, null, "Отчёты");
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, structure.OrganizationId, reports.Id, "Квартальные");
        await OrganizationsTestData.LinkAreaToProjectAsync(factory, structure.OrganizationId, reports.Id, structure.ProjectId);
        await OrganizationsTestData.LinkAreaToProjectAsync(factory, structure.OrganizationId, quarterly.Id, structure.ProjectId);
        await OrganizationsTestData.SetDefaultTeamAsync(factory, structure.OrganizationId, structure.ProjectId, structure.Platform.Id);
        var before = await ReadBeneathAsync(structure.ProjectId);

        var answer = await DeactivateAsync(structure);

        var after = await ReadBeneathAsync(structure.ProjectId);
        var project = await ReadProjectAsync(structure.ProjectId);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal(3, after.Teams.Count);
        Assert.Equal(2, after.Areas.Count);
        Assert.Equal(before.Teams, after.Teams);
        Assert.Equal(before.Areas, after.Areas);
        Assert.Equal(new ProjectState("Пример проекта", "PRM", ProjectStatus.Deactivated), project);
        Assert.Equal(ProjectStatus.Active, (await ReadProjectAsync(sibling.Project.Id)).Status);
        Assert.Equal(OrganizationStatus.Active, await ReadOrganizationStatusAsync(structure.OrganizationId));
    }

    private static IEnumerable<Guid> Ids(JsonElement list) =>
        list.EnumerateArray().Select(item => item.GetProperty("id").GetGuid());

    private async Task<ProjectState> ReadProjectAsync(Guid projectId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        var project = await db.Projects.AsNoTracking().SingleAsync(row => row.Id == projectId);

        return new ProjectState(project.Name, project.Key, project.Status);
    }

    private async Task<OrganizationStatus> ReadOrganizationStatusAsync(Guid organizationId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

        return (await db.Organizations.AsNoTracking().SingleAsync(row => row.Id == organizationId)).Status;
    }

    /// <summary>The project's teams and areas as stored now, whatever the project's status, one line each.</summary>
    private async Task<Beneath> ReadBeneathAsync(Guid projectId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        var project = await db.Projects.AsNoTracking().SingleAsync(row => row.Id == projectId);
        var teams = await db.Teams.AsNoTracking().Where(team => db.ProjectTeams.Any(link => link.ProjectId == projectId && link.TeamId == team.Id)).OrderBy(team => team.Id).ToListAsync();
        var areas = await db.Areas.AsNoTracking().Where(area => db.AreaProjects.Any(link => link.ProjectId == projectId && link.AreaId == area.Id)).OrderBy(area => area.Id).ToListAsync();

        return new Beneath(
            [.. teams.Select(team => $"{team.Id} {team.Name} {team.Status}"), $"setting {project.DefaultTeamId}"],
            [.. areas.Select(area => $"{area.Id} parent={area.ParentId} {area.Name}")]);
    }

    private sealed record ProjectState(string Name, string Key, ProjectStatus Status);

    private sealed record Beneath(IReadOnlyList<string> Teams, IReadOnlyList<string> Areas);
}
