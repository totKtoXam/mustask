using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/restore</c>
/// (api-contract § 2.7) through the real host and a real PostgreSQL. Every test
/// builds its own organization (<see cref="OrganizationsTestData"/>): the
/// database is shared and never emptied. What the restore left behind is read
/// through the module's in-process seam and the tree read, whatever the
/// project's status, so no other lane's route is needed.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class RestoreProjectEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> RestoreAsync(Guid organizationId, string projectId, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            $"{OrganizationsApi.Organization(organizationId)}/projects/{Uri.EscapeDataString(projectId)}/restore",
            token: token ?? OrganizationsTokens.SignedIn());

    private Task<Answer> RestoreAsync(OrganizationStructure structure) =>
        RestoreAsync(structure.OrganizationId, structure.ProjectId.ToString());

    // US-3 · "A deactivated project is restored exactly as it was": back with its name, key, teams, default areas and area
    // tree as before. The structure has a second team with an explicitly owned child, a child that inherits, and a team
    // deleted before the deactivation (its area passed to the first team), which stays deleted.
    [Fact]
    public async Task Restore_BringsBackKeyTeamsDefaultsAndTree()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(
            factory, structure.ProjectId, structure.RootId, "Отчёты", structure.Platform.Team.Id);
        await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, reports.Id, "Квартальные");
        await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Архив");
        var core = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Ядро");
        await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, core.Area.Id, "Сервисы", core.Team.Id);
        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, core.Team.Id);
        var before = await SnapshotAsync(structure);
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);
        Assert.False((await ReadProjectAsync(structure.ProjectId)).IsReachable);

        var answer = await RestoreAsync(structure);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(["id", "key", "name", "organizationId"], PropertyNames(answer.Body));
        Assert.Equal(structure.ProjectId, answer.Body.GetProperty("id").GetGuid());
        Assert.Equal(structure.OrganizationId, answer.Body.GetProperty("organizationId").GetGuid());
        Assert.Equal("Пример проекта", answer.Body.GetProperty("name").GetString());
        Assert.Equal("PRM", answer.Body.GetProperty("key").GetString());

        var restored = await ReadProjectAsync(structure.ProjectId);
        Assert.Equal(ProjectStatus.Active, restored.Status);
        Assert.True(restored.IsReachable);
        Assert.Equal(before, await SnapshotAsync(structure));
        Assert.Equal(TeamStatus.Deleted, (await ReadTeamAsync(core.Team.Id)).Status);
        Assert.Equal(TeamStatus.Active, (await ReadTeamAsync(structure.Platform.Team.Id)).Status);
    }

    // api-contract § 2.7 · "Restoring an active project changes nothing and answers 200": the same shape, nothing written.
    [Fact]
    public async Task Restore_ActiveProject_IsANoOp()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var before = await SnapshotAsync(structure);

        var first = await RestoreAsync(structure);
        var second = await RestoreAsync(structure);

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(HttpStatusCode.OK, second.Status);
        Assert.Equal(structure.ProjectId, first.Body.GetProperty("id").GetGuid());
        Assert.Equal(first.Body.GetRawText(), second.Body.GetRawText());
        Assert.Equal(ProjectStatus.Active, (await ReadProjectAsync(structure.ProjectId)).Status);
        Assert.Equal(before, await SnapshotAsync(structure));
    }

    // ADR-0026 rule 2 · a restore writes one row: another deactivated project of the same organization stays deactivated.
    [Fact]
    public async Task Restore_WritesOnlyThatProject_AnotherDeactivatedProjectStays()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateProjectAsync(
            factory, structure.OrganizationId, "Отчётность", "RPT", ProjectStatus.Deactivated);
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);

        var answer = await RestoreAsync(structure);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(ProjectStatus.Active, (await ReadProjectAsync(structure.ProjectId)).Status);
        Assert.Equal(ProjectStatus.Deactivated, (await ReadProjectAsync(other.Project.Id)).Status);
    }

    // US-3 · restore "needs every container above it reachable" (ADR-0026 rule 5, api-contract § 2.7): under a deactivated
    // organization the answer is organization.not_found, whatever the project's status, and nothing changes.
    [Theory]
    [InlineData(ProjectStatus.Deactivated)]
    [InlineData(ProjectStatus.Active)]
    public async Task Restore_UnderDeactivatedOrganization_Returns404OrganizationNotFound(ProjectStatus projectStatus)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, projectStatus);
        await OrganizationsTestData.SetOrganizationStatusAsync(factory, structure.OrganizationId, OrganizationStatus.Deactivated);

        var answer = await RestoreAsync(structure);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.organization.not_found", answer.ErrorCode);
        Assert.Equal("Организация не найдена.", answer.Detail);
        Assert.Equal(projectStatus, (await ReadProjectAsync(structure.ProjectId)).Status);
        Assert.Equal(OrganizationStatus.Deactivated, (await ReadOrganizationAsync(structure.OrganizationId)).Status);
    }

    // US-2 · BR-M1-20: a project deactivated on its own stays so when its organization comes back; it is restored on its own.
    [Fact]
    public async Task Restore_AfterTheOrganizationIsRestored_RestoresTheProjectDeactivatedOnItsOwn()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);
        await OrganizationsTestData.SetOrganizationStatusAsync(factory, structure.OrganizationId, OrganizationStatus.Deactivated);
        Assert.Equal(HttpStatusCode.NotFound, (await RestoreAsync(structure)).Status);

        await OrganizationsTestData.SetOrganizationStatusAsync(factory, structure.OrganizationId, OrganizationStatus.Active);
        var stillDeactivated = await ReadProjectAsync(structure.ProjectId);
        var answer = await RestoreAsync(structure);

        Assert.Equal(ProjectStatus.Deactivated, stillDeactivated.Status);
        Assert.False(stillDeactivated.IsReachable);
        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var restored = await ReadProjectAsync(structure.ProjectId);
        Assert.Equal(ProjectStatus.Active, restored.Status);
        Assert.True(restored.IsReachable);
    }

    // US-1 · "A visitor who is not signed in sees nothing": the challenge, and the project stays deactivated.
    [Fact]
    public async Task Restore_NoToken_Returns401_ChangesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);

        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{OrganizationsApi.Project(structure.OrganizationId, structure.ProjectId)}/restore");

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal("http.unauthorized", answer.ErrorCode);
        Assert.Equal(ProjectStatus.Deactivated, (await ReadProjectAsync(structure.ProjectId)).Status);
    }

    // US-3 · "A project identifier that names no project of the organization" (BR-M1-20, BR-M1-22): malformed, unknown, or
    // the project of another organization answers project.not_found, and the other organization's project is not touched.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    public async Task Restore_ProjectNotOfOrganization_Returns404ProjectNotFound(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.SetProjectStatusAsync(factory, other.OrganizationId, other.ProjectId, ProjectStatus.Deactivated);
        var projectId = how switch
        {
            "malformed" => "PRM",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => other.ProjectId.ToString(),
        };

        var answer = await RestoreAsync(structure.OrganizationId, projectId);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal("Проект не найден.", answer.Detail);
        Assert.Equal(ProjectStatus.Deactivated, (await ReadProjectAsync(other.ProjectId)).Status);
        Assert.Equal(ProjectStatus.Active, (await ReadProjectAsync(structure.ProjectId)).Status);
    }

    // api-contract § 0.2 · the server accepts the canonical form of an id in either letter case.
    [Fact]
    public async Task Restore_IdsInUpperCase_Returns200_ResponseWritesLowercase()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            $"/api/organizations/{structure.OrganizationId.ToString().ToUpperInvariant()}/projects/{structure.ProjectId.ToString().ToUpperInvariant()}/restore",
            token: OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(structure.ProjectId.ToString(), answer.Body.GetProperty("id").GetString());
        Assert.Equal(ProjectStatus.Active, (await ReadProjectAsync(structure.ProjectId)).Status);
    }

    /// <summary>
    /// What lies beneath the project, as stored, whatever the project's status: its name, key and first team and root, its
    /// active teams with their default areas, and the whole area tree in pre-order. The project's own status and its
    /// reachability are left out on purpose: they are what the restore changes.
    /// </summary>
    private async Task<string> SnapshotAsync(OrganizationStructure structure)
    {
        using var scope = factory.Services.CreateScope();
        var seam = scope.ServiceProvider.GetRequiredService<IOrganizationStructure>();
        var project = await seam.FindProjectAsync(structure.ProjectId, CancellationToken.None)
            ?? throw new InvalidOperationException("The project is gone.");
        var teams = await seam.ListActiveTeamsAsync(structure.ProjectId, CancellationToken.None);
        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);

        var lines = new List<string>
        {
            $"project {project.Id}|{project.OrganizationId}|{project.Name}|{project.Key}|{project.FirstTeamId}|{project.RootAreaId}|{project.CreatedBy}|{project.CreatedAt:O}",
        };
        lines.AddRange(teams.Select(team => $"team {team.Id}|{team.Name}|{team.Status}|{team.IsFirstTeam}|{team.DefaultAreaId}"));
        lines.AddRange(tree.PreOrder().Select(node => $"area {node.Id}|{node.ParentId}|{node.Name}|{node.OwnerTeamId}|{node.DefaultOfTeamId}"));

        return string.Join(Environment.NewLine, lines);
    }

    private async Task<ProjectInfo> ReadProjectAsync(Guid projectId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IOrganizationStructure>().FindProjectAsync(projectId, CancellationToken.None)
            ?? throw new InvalidOperationException("The project is gone.");
    }

    private async Task<TeamInfo> ReadTeamAsync(Guid teamId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IOrganizationStructure>().FindTeamAsync(teamId, CancellationToken.None)
            ?? throw new InvalidOperationException("The team is gone.");
    }

    private async Task<OrganizationInfo> ReadOrganizationAsync(Guid organizationId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IOrganizationStructure>().FindOrganizationAsync(organizationId, CancellationToken.None)
            ?? throw new InvalidOperationException("The organization is gone.");
    }

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);
}
